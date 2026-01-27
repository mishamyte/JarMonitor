module JarMonitor.ApiClient

open System
open System.Text.Json.Serialization
open FsHttp

[<CLIMutable>]
type JarResponse =
    {
        [<JsonPropertyName("jarGoal")>]
        JarGoal: int64 option
        [<JsonPropertyName("jarAmount")>]
        JarAmount: int64
        [<JsonPropertyName("name")>]
        Name: string option
        [<JsonPropertyName("jarStatus")>]
        JarStatus: string option
    }

let private apiUrl = "https://send.monobank.ua/api/handler"

let private userAgent =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"

let fetchJarData (jarId: string) : Async<Result<JarResponse, string>> =
    let rec attempt retryCount =
        async {
            try
                let response =
                    http {
                        POST apiUrl
                        UserAgent userAgent
                        body
                        jsonSerialize {| c = "hello"; clientId = jarId; referer = ""; Pc = "hello" |}
                    }
                    |> Request.send

                if response.statusCode = System.Net.HttpStatusCode.OK then
                    let jarResponse = response |> Response.deserializeJson<JarResponse>
                    return Ok jarResponse
                else
                    if retryCount < 3 then
                        let delayMs = int (Math.Pow(2.0, float retryCount)) * 1000
                        do! Async.Sleep delayMs
                        return! attempt (retryCount + 1)
                    else
                        let! errorBody = response.content.ReadAsStringAsync() |> Async.AwaitTask
                        return Error $"HTTP {int response.statusCode}: {errorBody}"
            with ex ->
                if retryCount < 3 then
                    let delayMs = int (Math.Pow(2.0, float retryCount)) * 1000
                    do! Async.Sleep delayMs
                    return! attempt (retryCount + 1)
                else
                    return Error $"Request failed: {ex.Message}"
        }

    attempt 0

let amountToUah (kopiykas: int64) : decimal = decimal kopiykas / 100m

let formatAmount (kopiykas: int64) : string =
    let uah = amountToUah kopiykas
    uah.ToString("N0") + " ₴"
