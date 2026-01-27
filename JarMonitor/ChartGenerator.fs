module JarMonitor.ChartGenerator

open System
open System.IO
open SkiaSharp
open Svg.Skia

type ChartData =
    {
        JarName: string
        Color: string
        Points: (DateTime * int64) list
    }

type ProgressBarData =
    {
        Name: string
        Current: int64
        Goal: int64
        Delta: int64
    }

let private colors =
    [| "#4CAF50"; "#2196F3"; "#FF9800"; "#E91E63"; "#9C27B0"; "#00BCD4" |]

let private formatDateLabel (date: DateTime) = date.ToString("dd.MM")

let private formatAmountLabel (amount: int64) =
    let uah = decimal amount / 100m

    if uah >= 1000000m then
        $"%.1f{float uah / 1000000.0}M"
    elif uah >= 1000m then
        let k = float uah / 1000.0
        if k = floor k then $"%.0f{k}K" else $"%.1f{k}K"
    else
        $"%.0f{float uah}"

let generateSvg (data: ChartData list) (width: int) (height: int) : string =
    if data.IsEmpty || data |> List.forall _.Points.IsEmpty then
        sprintf
            """<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d">
            <rect width="100%%" height="100%%" fill="#1a1a2e"/>
            <text x="50%%" y="50%%" fill="#ffffff" text-anchor="middle" font-family="Arial" font-size="16">Немає даних</text>
        </svg>"""
            width
            height
    else

        let margin =
            {|
                Left = 70
                Right = 20
                Top = 30
                Bottom = 60
            |}

        let chartWidth = width - margin.Left - margin.Right
        let chartHeight = height - margin.Top - margin.Bottom

        let allPoints = data |> List.collect _.Points
        let allDates = allPoints |> List.map fst |> List.distinct |> List.sort
        let allAmounts = allPoints |> List.map snd

        let minDate = allDates |> List.min
        let maxDate = allDates |> List.max
        let minAmount = 0L
        let maxAmount = (allAmounts |> List.max |> float) * 1.1 |> int64

        let dateRange = (maxDate - minDate).TotalDays |> max 1.0
        let amountRange = float (maxAmount - minAmount) |> max 1.0

        let toX (date: DateTime) =
            let days = (date - minDate).TotalDays
            margin.Left + int (days / dateRange * float chartWidth)

        let toY (amount: int64) =
            let normalized = float (amount - minAmount) / amountRange
            margin.Top + chartHeight - int (normalized * float chartHeight)

        let sb = System.Text.StringBuilder()

        // SVG header
        sb.AppendLine $"""<svg xmlns="http://www.w3.org/2000/svg" width="%d{width}" height="%d{height}">"""
        |> ignore

        // Background
        sb.AppendLine("""  <rect width="100%" height="100%" fill="#1a1a2e"/>""")
        |> ignore

        // Grid lines (horizontal)
        let gridLines = 5

        for i in 0..gridLines do
            let y = margin.Top + (i * chartHeight / gridLines)

            let amount =
                maxAmount - int64 (float i / float gridLines * float (maxAmount - minAmount))

            sb.AppendLine
                $"""  <line x1="%d{margin.Left}" y1="%d{y}" x2="%d{width - margin.Right}" y2="%d{y}" stroke="#333355" stroke-width="1"/>"""
            |> ignore

            sb.AppendLine
                $"""  <text x="%d{margin.Left - 5}" y="%d{y + 4}" fill="#888888" font-family="Arial" font-size="10" text-anchor="end">%s{formatAmountLabel amount}</text>"""
            |> ignore

        // X-axis labels (show every few days depending on range)
        let labelStep = max 1 (allDates.Length / 7)

        for i in 0..labelStep .. (allDates.Length - 1) do
            let date = allDates[i]
            let x = toX date

            sb.AppendLine
                $"""  <text x="%d{x}" y="%d{height - margin.Bottom + 20}" fill="#888888" font-family="Arial" font-size="10" text-anchor="middle">%s{formatDateLabel date}</text>"""
            |> ignore

        // Draw lines and points for each jar
        for i, chartData in data |> List.indexed do
            let color = colors[i % colors.Length]

            if chartData.Points.Length > 0 then
                let sortedPoints = chartData.Points |> List.sortBy fst

                // Draw line only if we have more than 1 point
                if sortedPoints.Length > 1 then
                    let pathPoints =
                        sortedPoints |> List.map (fun (date, amount) -> $"%d{toX date},%d{toY amount}")

                    let pathData = "M " + String.Join(" L ", pathPoints)

                    sb.AppendLine $"""  <path d="%s{pathData}" fill="none" stroke="%s{color}" stroke-width="2"/>"""
                    |> ignore

                // Always draw points (even with just 1 point)
                for date, amount in sortedPoints do
                    sb.AppendLine $"""  <circle cx="%d{toX date}" cy="%d{toY amount}" r="4" fill="%s{color}"/>"""
                    |> ignore

        // Legend
        let legendY = 15

        for i, chartData in data |> List.indexed do
            let color = colors[i % colors.Length]
            let legendX = margin.Left + (i * 120)

            sb.AppendLine $"""  <rect x="%d{legendX}" y="%d{legendY - 10}" width="12" height="12" fill="%s{color}"/>"""
            |> ignore

            sb.AppendLine
                $"""  <text x="%d{legendX + 16}" y="%d{legendY}" fill="#ffffff" font-family="Arial" font-size="11">%s{chartData.JarName}</text>"""
            |> ignore

        sb.AppendLine("</svg>") |> ignore
        sb.ToString()

let generateProgressBarSvg (data: ProgressBarData list) (width: int) (height: int) : string =
    if data.IsEmpty then
        sprintf
            """<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d">
            <rect width="100%%" height="100%%" fill="#1a1a2e"/>
            <text x="50%%" y="50%%" fill="#ffffff" text-anchor="middle" font-family="Arial" font-size="16">Немає даних</text>
        </svg>"""
            width
            height
    else
        let barHeight = 32
        let barSpacing = 20

        let margin =
            {|
                Left = 20
                Right = 20
                Top = 25
                Bottom = 15
            |}

        let barWidth = width - margin.Left - margin.Right - 180 // Leave space for labels on the right

        let sb = System.Text.StringBuilder()

        // SVG header
        sb.AppendLine $"""<svg xmlns="http://www.w3.org/2000/svg" width="%d{width}" height="%d{height}">"""
        |> ignore

        // Background
        sb.AppendLine("""  <rect width="100%" height="100%" fill="#1a1a2e"/>""")
        |> ignore

        // Draw each progress bar
        for i, bar in data |> List.indexed do
            let y = margin.Top + i * (barHeight + barSpacing)

            let percent =
                if bar.Goal > 0L then
                    float bar.Current / float bar.Goal
                else
                    0.0

            let filledWidth = int (float barWidth * (min 1.0 percent))

            // Calculate delta portion
            let previousAmount = bar.Current - bar.Delta

            let previousPercent =
                if bar.Goal > 0L then
                    float previousAmount / float bar.Goal
                else
                    0.0

            let previousWidth = int (float barWidth * (min 1.0 (max 0.0 previousPercent)))
            let deltaWidth = filledWidth - previousWidth

            let color = colors[i % colors.Length]
            // Darker shade for "previous" portion
            let darkColor =
                match color with
                | "#4CAF50" -> "#2E7D32"
                | "#2196F3" -> "#1565C0"
                | "#FF9800" -> "#E65100"
                | "#E91E63" -> "#AD1457"
                | "#9C27B0" -> "#6A1B9A"
                | "#00BCD4" -> "#00838F"
                | _ -> "#444466"

            // Jar name (above bar)
            sb.AppendLine
                $"""  <text x="%d{margin.Left}" y="%d{y - 5}" fill="#ffffff" font-family="Arial" font-size="13" font-weight="bold">%s{bar.Name}</text>"""
            |> ignore

            // Background bar (empty portion)
            sb.AppendLine
                $"""  <rect x="%d{margin.Left}" y="%d{y}" width="%d{barWidth}" height="%d{barHeight}" fill="#333355" rx="4"/>"""
            |> ignore

            // Previous amount portion (dark color)
            if previousWidth > 0 then
                sb.AppendLine
                    $"""  <rect x="%d{margin.Left}" y="%d{y}" width="%d{previousWidth}" height="%d{barHeight}" fill="%s{darkColor}" rx="4"/>"""
                |> ignore

            // Delta portion (bright color)
            if deltaWidth > 0 then
                sb.AppendLine
                    $"""  <rect x="%d{margin.Left + previousWidth}" y="%d{y}" width="%d{deltaWidth}" height="%d{barHeight}" fill="%s{color}" rx="4"/>"""
                |> ignore

            // Percentage text
            let pctStr = $"%.0f{percent * 100.0}%%"
            let pctX = margin.Left + barWidth + 10

            sb.AppendLine
                $"""  <text x="%d{pctX}" y="%d{y + barHeight / 2 + 5}" fill="#ffffff" font-family="Arial" font-size="14" font-weight="bold">%s{pctStr}</text>"""
            |> ignore

            // Amount text (current / goal)
            let currentStr = formatAmountLabel bar.Current
            let goalStr = formatAmountLabel bar.Goal
            let amountX = pctX + 45

            sb.AppendLine
                $"""  <text x="%d{amountX}" y="%d{y + barHeight / 2 + 5}" fill="#aaaaaa" font-family="Arial" font-size="12">%s{currentStr} / %s{goalStr}</text>"""
            |> ignore

            // Delta text
            let deltaStr =
                if bar.Delta > 0L then $"+{formatAmountLabel bar.Delta}"
                elif bar.Delta < 0L then formatAmountLabel bar.Delta
                else "±0"

            let deltaX = amountX + 85
            let deltaColor = if bar.Delta >= 0L then "#4CAF50" else "#F44336"

            sb.AppendLine
                $"""  <text x="%d{deltaX}" y="%d{y + barHeight / 2 + 5}" fill="%s{deltaColor}" font-family="Arial" font-size="12" font-weight="bold">%s{deltaStr}</text>"""
            |> ignore

        sb.AppendLine("</svg>") |> ignore
        sb.ToString()

let svgToPng (svgContent: string) : byte[] =
    use svg = new SKSvg()
    use stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent))
    let picture = svg.Load(stream)

    if picture = null then
        // Return a simple error image
        use errorBitmap = new SKBitmap(400, 100)
        use canvas = new SKCanvas(errorBitmap)
        canvas.Clear(SKColors.DarkRed)
        use font = new SKFont(SKTypeface.Default, 16.0f)
        use paint = new SKPaint(Color = SKColors.White)
        canvas.DrawText("Error generating chart", 20.0f, 50.0f, font, paint)
        use image = SKImage.FromBitmap(errorBitmap)
        use data = image.Encode(SKEncodedImageFormat.Png, 100)
        data.ToArray()
    else
        let bounds = svg.Picture.CullRect
        let width = int bounds.Width
        let height = int bounds.Height

        use bitmap = new SKBitmap(width, height)
        use canvas = new SKCanvas(bitmap)
        canvas.Clear(SKColors.Transparent)
        canvas.DrawPicture(picture)

        use image = SKImage.FromBitmap(bitmap)
        use data = image.Encode(SKEncodedImageFormat.Png, 100)
        data.ToArray()

let generateChart (jars: ReportGenerator.JarReport list) : byte[] =
    let progressData =
        jars
        |> List.choose (fun jar ->
            match jar.Goal with
            | Some goal when goal > 0L ->
                Some
                    {
                        Name = jar.Name
                        Current = jar.CurrentAmount
                        Goal = goal
                        Delta = jar.DailyChange
                    }
            | _ -> None)

    let barCount = progressData.Length
    let height = max 150 (25 + barCount * 52 + 15) // Dynamic height based on number of bars

    let svg = generateProgressBarSvg progressData 800 height
    svgToPng svg
