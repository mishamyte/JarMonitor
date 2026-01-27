module JarMonitor.TelegramNotifier

open System
open System.IO
open Funogram.Api
open Funogram.Telegram
open Funogram.Telegram.Bot
open Funogram.Telegram.Types

let private createConfig (botToken: string) =
    { Config.defaultConfig with
        Token = botToken
    }

let sendMessage (botToken: string) (chatId: string) (text: string) : Async<Result<unit, string>> =
    async {
        try
            let config = createConfig botToken

            let chatIdValue =
                match Int64.TryParse(chatId) with
                | true, id -> ChatId.Int id
                | false, _ -> ChatId.String chatId

            let request =
                Req.SendMessage.Make(chatId = chatIdValue, text = text, parseMode = ParseMode.MarkdownV2)

            let! result = api config request

            match result with
            | Ok _ -> return Ok()
            | Error e -> return Error $"Telegram API error: {e.Description}"
        with ex ->
            return Error $"Failed to send message: {ex.Message}"
    }

let sendPhoto
    (botToken: string)
    (chatId: string)
    (pngBytes: byte[])
    (caption: string option)
    : Async<Result<unit, string>> =
    async {
        try
            let config = createConfig botToken

            let chatIdValue =
                match Int64.TryParse(chatId) with
                | true, id -> ChatId.Int id
                | false, _ -> ChatId.String chatId

            use memStream = new MemoryStream(pngBytes)
            let inputFile = InputFile.File("chart.png", memStream)

            let request =
                match caption with
                | Some cap ->
                    Req.SendPhoto.Make(
                        chatId = chatIdValue,
                        photo = inputFile,
                        caption = cap,
                        parseMode = ParseMode.MarkdownV2
                    )
                | None -> Req.SendPhoto.Make(chatId = chatIdValue, photo = inputFile)

            let! result = api config request

            match result with
            | Ok _ -> return Ok()
            | Error e -> return Error $"Telegram API error: {e.Description}"
        with ex ->
            return Error $"Failed to send photo: {ex.Message}"
    }

let sendReport (botToken: string) (chatId: string) (text: string) (chartPng: byte[]) : Async<Result<unit, string>> =
    async {
        // Send photo with caption
        let! photoResult = sendPhoto botToken chatId chartPng (Some text)

        match photoResult with
        | Ok() -> return Ok()
        | Error photoErr ->
            // If photo fails, try sending just the message
            printfn $"Warning: Failed to send photo ({photoErr}), sending text only"
            return! sendMessage botToken chatId text
    }
