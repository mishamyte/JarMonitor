module JarMonitor.Program

open System
open Cronos
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Serilog

[<Literal>]
let DataPath = "data/history.json"

let calculateNextRun
    (logger: Microsoft.Extensions.Logging.ILogger)
    (scheduleTime: string)
    (timezone: TimeZoneInfo)
    : DateTime =
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
        logger.LogWarning("Failed to parse schedule time '{ScheduleTime}': {Error}", scheduleTime, ex.Message)
        DateTime.UtcNow.AddDays(1.0)

let runReport (logger: Microsoft.Extensions.Logging.ILogger) (config: Config.AppConfig) : Async<unit> =
    async {
        logger.LogInformation("Starting report generation...")

        let timezone = Config.getTimeZone config
        let localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone)
        let today = localNow.ToString("yyyy-MM-dd")

        let mutable historyData = DataStore.load DataPath

        let! jarResults =
            config.Jars
            |> Array.map (fun jarConfig ->
                async {
                    logger.LogInformation("Fetching jar: {JarName} ({JarId})", jarConfig.Name, jarConfig.JarId)
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
                    logger.LogError("Error fetching {JarName}: {Error}", jarConfig.Name, err)
                    None)
            |> Array.toList

        if successfulJars.IsEmpty then
            logger.LogWarning("No jar data available, skipping report")
        else
            match DataStore.save DataPath historyData with
            | Ok() -> logger.LogInformation("History saved")
            | Error err -> logger.LogWarning("Failed to save history: {Error}", err)

            let report = ReportGenerator.generate successfulJars localNow

            logger.LogInformation("Report generated:\n{Report}", ReportGenerator.formatConsole report)

            let chartPng = ChartGenerator.generateChart report.Jars
            logger.LogInformation("Generated chart: {ChartSize} bytes", chartPng.Length)

            let telegramText = ReportGenerator.formatTelegram report
            logger.LogInformation("Sending to Telegram...")

            let! sendResult =
                TelegramNotifier.sendReport config.Telegram.BotToken config.Telegram.ChannelId telegramText chartPng

            match sendResult with
            | Ok() -> logger.LogInformation("Report sent successfully!")
            | Error err -> logger.LogError("Failed to send report: {Error}", err)

        logger.LogInformation("Report generation complete")
    }

let run (logger: Microsoft.Extensions.Logging.ILogger) (config: Config.AppConfig) (runNow: bool) : Async<int> =
    async {
        logger.LogInformation("JarMonitor starting...")
        logger.LogInformation("Loaded {JarCount} jar(s)", config.Jars.Length)
        logger.LogInformation("Schedule: {ScheduleTime}", config.ScheduleTime)

        if runNow then
            do! runReport logger config
            return 0
        else
            let timezone = Config.getTimeZone config

            while true do
                let nextRun = calculateNextRun logger config.ScheduleTime timezone
                let delay = nextRun - DateTime.UtcNow

                if delay > TimeSpan.Zero then
                    let localNext = TimeZoneInfo.ConvertTimeFromUtc(nextRun, timezone)
                    logger.LogInformation("Next run scheduled for {NextRun:u} (in {Delay})", localNext, delay)
                    do! Async.Sleep(int delay.TotalMilliseconds)

                do! runReport logger config
                do! Async.Sleep(60000)

            return 0
    }

[<EntryPoint>]
let main args =
    let runNow = args |> Array.contains "--run-now"

    let builder = Host.CreateApplicationBuilder(args)

    // Configure Serilog from configuration
    Log.Logger <- LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger()

    // Clear default providers and use Serilog
    builder.Logging.ClearProviders() |> ignore
    builder.Services.AddSerilog() |> ignore

    let host = builder.Build()

    let configuration = host.Services.GetRequiredService<IConfiguration>()
    let loggerFactory = host.Services.GetRequiredService<ILoggerFactory>()
    let logger = loggerFactory.CreateLogger("JarMonitor")

    match Config.bindConfig configuration with
    | Error err ->
        logger.LogError("Configuration error: {Error}", err)
        1
    | Ok config -> Async.RunSynchronously(run logger config runNow)
