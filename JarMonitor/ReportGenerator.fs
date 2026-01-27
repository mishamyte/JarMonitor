module JarMonitor.ReportGenerator

open System
open JarMonitor.ApiClient

type JarReport =
    {
        Name: string
        JarId: string
        CurrentAmount: int64
        Goal: int64 option
        DailyChange: int64
        ProgressPercent: float option
    }

type DailyReport =
    {
        Date: string
        Jars: JarReport list
        TotalAmount: int64
        TotalDailyChange: int64
    }

let generate
    (jars: (Config.JarConfig * JarResponse * DataStore.DailyRecord option) list)
    (date: DateTime)
    : DailyReport =
    let jarReports =
        jars
        |> List.map (fun (config, response, yesterday) ->
            let dailyChange =
                match yesterday with
                | Some y -> response.JarAmount - y.Amount
                | None -> response.JarAmount // First run: treat current as today's gain

            let progressPercent =
                response.JarGoal
                |> Option.map (fun goal ->
                    if goal > 0L then
                        (float response.JarAmount / float goal) * 100.0
                    else
                        0.0)

            {
                Name = config.Name
                JarId = config.JarId
                CurrentAmount = response.JarAmount
                Goal = response.JarGoal
                DailyChange = dailyChange
                ProgressPercent = progressPercent
            })

    let totalAmount = jarReports |> List.sumBy _.CurrentAmount

    let totalDailyChange = jarReports |> List.sumBy _.DailyChange

    {
        Date = date.ToString("dd.MM.yyyy")
        Jars = jarReports
        TotalAmount = totalAmount
        TotalDailyChange = totalDailyChange
    }

let progressBar (percent: float) : string =
    let filled = int (percent / 10.0) |> max 0 |> min 10
    let empty = 10 - filled
    String.replicate filled "▓" + String.replicate empty "░"

let private formatChange (change: int64) : string =
    if change > 0L then $"+{formatAmount change}"
    elif change < 0L then formatAmount change
    else "±0 ₴"

let formatConsole (report: DailyReport) : string =
    let sb = System.Text.StringBuilder()
    sb.AppendLine($"=== Звіт за {report.Date} ===") |> ignore
    sb.AppendLine() |> ignore

    for jar in report.Jars do
        sb.AppendLine($"📦 {jar.Name}") |> ignore
        sb.AppendLine($"   Зібрано: {formatAmount jar.CurrentAmount}") |> ignore

        match jar.Goal with
        | Some goal ->
            sb.AppendLine($"   Мета: {formatAmount goal}") |> ignore

            match jar.ProgressPercent with
            | Some pct ->
                let pctStr = $"%.1f{pct}"
                sb.AppendLine($"   Прогрес: [{progressBar pct}] {pctStr}%%") |> ignore
            | None -> ()
        | None -> ()

        sb.AppendLine($"   За добу: {formatChange jar.DailyChange}") |> ignore
        sb.AppendLine() |> ignore

    sb.AppendLine($"💰 Всього: {formatAmount report.TotalAmount}") |> ignore

    sb.AppendLine($"📈 Всього за добу: {formatChange report.TotalDailyChange}")
    |> ignore

    sb.ToString()

let escapeMarkdownV2 (text: string) : string =
    let specialChars =
        [
            '_'
            '*'
            '['
            ']'
            '('
            ')'
            '~'
            '`'
            '>'
            '#'
            '+'
            '-'
            '='
            '|'
            '{'
            '}'
            '.'
            '!'
        ]

    specialChars
    |> List.fold (fun (s: string) c -> s.Replace(string c, "\\" + string c)) text

let formatTelegram (report: DailyReport) : string =
    let sb = System.Text.StringBuilder()
    let escaped = escapeMarkdownV2

    sb.AppendLine($"*Звіт за {escaped report.Date}*") |> ignore
    sb.AppendLine() |> ignore

    for jar in report.Jars do
        sb.AppendLine($"📦 *{escaped jar.Name}*") |> ignore

        sb.AppendLine($"Зібрано: `{escaped (formatAmount jar.CurrentAmount)}`")
        |> ignore

        match jar.Goal with
        | Some goal ->
            sb.AppendLine($"Мета: `{escaped (formatAmount goal)}`") |> ignore

            match jar.ProgressPercent with
            | Some pct ->
                let pctStr = $"%.1f{pct}"

                sb.AppendLine($"Прогрес: `\\[{progressBar pct}\\]` {escaped pctStr}%%")
                |> ignore
            | None -> ()
        | None -> ()

        let changeStr = formatChange jar.DailyChange
        sb.AppendLine($"За добу: `{escaped changeStr}`") |> ignore
        sb.AppendLine() |> ignore

    sb.AppendLine($"💰 *Всього:* `{escaped (formatAmount report.TotalAmount)}`")
    |> ignore

    let totalChangeStr = formatChange report.TotalDailyChange
    sb.AppendLine($"📈 *За добу:* `{escaped totalChangeStr}`") |> ignore

    sb.ToString()
