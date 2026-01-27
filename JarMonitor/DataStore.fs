module JarMonitor.DataStore

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

[<CLIMutable>]
type DailyRecord =
    {
        [<JsonPropertyName("date")>]
        Date: string // ISO date format YYYY-MM-DD
        [<JsonPropertyName("amount")>]
        Amount: int64 // in kopiykas
        [<JsonPropertyName("goal")>]
        Goal: int64 option // in kopiykas
    }

[<CLIMutable>]
type JarHistory =
    {
        [<JsonPropertyName("jarId")>]
        JarId: string
        [<JsonPropertyName("name")>]
        Name: string
        [<JsonPropertyName("records")>]
        Records: DailyRecord array
    }

[<CLIMutable>]
type HistoryData =
    {
        [<JsonPropertyName("jars")>]
        Jars: JarHistory array
        [<JsonPropertyName("lastUpdated")>]
        LastUpdated: string
    }

let private jsonOptions =
    let options = JsonSerializerOptions()
    options.WriteIndented <- true
    options.PropertyNameCaseInsensitive <- true
    options

let private emptyHistory =
    {
        Jars = [||]
        LastUpdated = DateTime.UtcNow.ToString("o")
    }

let load (dataPath: string) : HistoryData =
    try
        if File.Exists(dataPath) then
            let json = File.ReadAllText(dataPath)
            JsonSerializer.Deserialize<HistoryData>(json, jsonOptions)
        else
            emptyHistory
    with _ ->
        emptyHistory

let save (dataPath: string) (data: HistoryData) : Result<unit, string> =
    try
        let dir = Path.GetDirectoryName(dataPath)

        if not (String.IsNullOrEmpty(dir)) && not (Directory.Exists(dir)) then
            Directory.CreateDirectory(dir) |> ignore

        let tempPath = dataPath + ".tmp"
        let json = JsonSerializer.Serialize(data, jsonOptions)
        File.WriteAllText(tempPath, json)

        if File.Exists(dataPath) then
            File.Delete(dataPath)

        File.Move(tempPath, dataPath)
        Ok()
    with ex ->
        Error $"Failed to save history: {ex.Message}"

let private maxDays = 90

let addRecord (jarId: string) (jarName: string) (record: DailyRecord) (data: HistoryData) : HistoryData =
    let cutoffDate = DateTime.UtcNow.AddDays(float -maxDays).ToString("yyyy-MM-dd")

    let existingJar = data.Jars |> Array.tryFind (fun j -> j.JarId = jarId)

    let updatedJar =
        match existingJar with
        | Some jar ->
            let filteredRecords =
                jar.Records
                |> Array.filter (fun r -> r.Date >= cutoffDate && r.Date <> record.Date)

            { jar with
                Name = jarName
                Records = Array.append filteredRecords [| record |] |> Array.sortBy _.Date
            }
        | None ->
            {
                JarId = jarId
                Name = jarName
                Records = [| record |]
            }

    let updatedJars =
        match existingJar with
        | Some _ -> data.Jars |> Array.map (fun j -> if j.JarId = jarId then updatedJar else j)
        | None -> Array.append data.Jars [| updatedJar |]

    {
        Jars = updatedJars
        LastUpdated = DateTime.UtcNow.ToString("o")
    }

let getPreviousRecord (jarId: string) (today: string) (data: HistoryData) : DailyRecord option =
    data.Jars
    |> Array.tryFind (fun j -> j.JarId = jarId)
    |> Option.bind (fun jar ->
        jar.Records
        |> Array.filter (fun r -> r.Date < today)
        |> Array.sortByDescending _.Date
        |> Array.tryHead)

let getRecentRecords (jarId: string) (days: int) (data: HistoryData) : DailyRecord array =
    let cutoffDate = DateTime.UtcNow.AddDays(float -days).ToString("yyyy-MM-dd")

    data.Jars
    |> Array.tryFind (fun j -> j.JarId = jarId)
    |> Option.map (fun jar ->
        jar.Records
        |> Array.filter (fun r -> r.Date >= cutoffDate)
        |> Array.sortBy _.Date)
    |> Option.defaultValue [||]
