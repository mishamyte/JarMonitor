module JarMonitor.Config

open System
open System.IO
open Microsoft.Extensions.Configuration

[<CLIMutable>]
type JarConfig = { Name: string; JarId: string }

[<CLIMutable>]
type TelegramConfig = { BotToken: string; ChannelId: string }

[<CLIMutable>]
type AppConfig =
    {
        Timezone: string
        ScheduleTime: string
        Jars: JarConfig array
        Telegram: TelegramConfig
    }

let load () : Result<AppConfig, string> =
    try
        let configuration =
            ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional = false)
                .AddEnvironmentVariables()
                .Build()

        let config =
            {
                Timezone = configuration["Timezone"] |> Option.ofObj |> Option.defaultValue "UTC"
                ScheduleTime = configuration["ScheduleTime"] |> Option.ofObj |> Option.defaultValue "00:00"
                Jars =
                    configuration.GetSection("Jars").GetChildren()
                    |> Seq.map (fun section ->
                        {
                            Name = section["Name"] |> Option.ofObj |> Option.defaultValue ""
                            JarId = section["JarId"] |> Option.ofObj |> Option.defaultValue ""
                        })
                    |> Seq.toArray
                Telegram =
                    {
                        BotToken = configuration["Telegram:BotToken"] |> Option.ofObj |> Option.defaultValue ""
                        ChannelId = configuration["Telegram:ChannelId"] |> Option.ofObj |> Option.defaultValue ""
                    }
            }

        Ok config
    with ex ->
        Error ex.Message

let getTimeZone (config: AppConfig) : TimeZoneInfo =
    try
        TimeZoneInfo.FindSystemTimeZoneById(config.Timezone)
    with :? TimeZoneNotFoundException ->
        // Try alternative names for common timezones
        match config.Timezone with
        | "Europe/Kyiv"
        | "Europe/Kiev" ->
            try
                TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time")
            with _ ->
                TimeZoneInfo.Utc
        | _ -> TimeZoneInfo.Utc
