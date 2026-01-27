module JarMonitor.Program

open System
open Cronos

[<Literal>]
let DataPath = "data/history.json"

let calculateNextRun (scheduleTime: string) (timezone: TimeZoneInfo) : DateTime =
    try
        let parts = scheduleTime.Split(':')
        let hour = int parts[0]
        let minute = if parts.Length > 1 then int parts[1] else 0
        let cronExpr = $"%d{minute} %d{hour} * * *"
        let cron = CronExpression.Parse(cronExpr)

        let nowUtc = DateTime.UtcNow
        let nextOccurrence = cron.GetNextOccurrence(nowUtc, timezone)

        if nextOccurrence.HasValue then
            nextOccurrence.Value
        else
            nowUtc.AddDays(1.0)
    with ex ->
        printfn $"Warning: Failed to parse schedule time '{scheduleTime}': {ex.Message}"
        DateTime.UtcNow.AddDays(1.0)

let runReport (config: Config.AppConfig) : Async<unit> =
    async {
        printfn $"[{DateTime.UtcNow:u}] Starting report generation..."

        let timezone = Config.getTimeZone config
        let localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone)
        let today = localNow.ToString("yyyy-MM-dd")

        let mutable historyData = DataStore.load DataPath

        let! jarResults =
            config.Jars
            |> Array.map (fun jarConfig ->
                async {
                    printfn $"  Fetching jar: {jarConfig.Name} ({jarConfig.JarId})"
                    let! result = ApiClient.fetchJarData jarConfig.JarId
                    return (jarConfig, result)
                })
            |> Async.Parallel

        let successfulJars =
            jarResults
            |> Array.choose (fun (jarConfig, result) ->
                match result with
                | Ok response ->
                    let yesterday = DataStore.getPreviousRecord jarConfig.JarId today historyData

                    let record: DataStore.DailyRecord =
                        {
                            Date = today
                            Amount = response.JarAmount
                            Goal = response.JarGoal
                        }

                    historyData <- DataStore.addRecord jarConfig.JarId jarConfig.Name record historyData
                    Some(jarConfig, response, yesterday)
                | Error err ->
                    printfn $"  Error fetching {jarConfig.Name}: {err}"
                    None)
            |> Array.toList

        if successfulJars.IsEmpty then
            printfn "  No jar data available, skipping report"
        else
            match DataStore.save DataPath historyData with
            | Ok() -> printfn "  History saved"
            | Error err -> printfn $"  Warning: Failed to save history: {err}"

            let report = ReportGenerator.generate successfulJars localNow

            printfn ""
            printfn $"%s{ReportGenerator.formatConsole report}"

            let chartPng = ChartGenerator.generateChart report.Jars
            printfn $"  Generated chart: {chartPng.Length} bytes"

            let telegramText = ReportGenerator.formatTelegram report
            printfn "  Sending to Telegram..."

            let! sendResult =
                TelegramNotifier.sendReport config.Telegram.BotToken config.Telegram.ChannelId telegramText chartPng

            match sendResult with
            | Ok() -> printfn "  Report sent successfully!"
            | Error err -> printfn $"  Failed to send report: {err}"

        printfn $"[{DateTime.UtcNow:u}] Report generation complete"
    }

let run (runNow: bool) : Async<int> =
    async {
        printfn "JarMonitor starting..."

        match Config.load () with
        | Error err ->
            printfn $"Error: {err}"
            return 1
        | Ok config ->
            printfn $"  Loaded {config.Jars.Length} jar(s)"
            printfn $"  Schedule: {config.ScheduleTime}"

            if runNow then
                do! runReport config
                return 0
            else
                let timezone = Config.getTimeZone config

                while true do
                    let nextRun = calculateNextRun config.ScheduleTime timezone
                    let delay = nextRun - DateTime.UtcNow

                    if delay > TimeSpan.Zero then
                        let localNext = TimeZoneInfo.ConvertTimeFromUtc(nextRun, timezone)
                        printfn $"[{DateTime.UtcNow:u}] Next run scheduled for {localNext:u} (in {delay})"
                        do! Async.Sleep(int delay.TotalMilliseconds)

                    do! runReport config
                    do! Async.Sleep(60000)

                return 0
    }

[<EntryPoint>]
let main args =
    let runNow = args |> Array.contains "--run-now"
    Async.RunSynchronously(run runNow)
