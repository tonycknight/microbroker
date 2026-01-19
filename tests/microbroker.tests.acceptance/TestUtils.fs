namespace microbroker.tests.acceptance

open System

module TestUtils =

    let client = new System.Net.Http.HttpClient()

    let jsonContent (json: string) =
        new Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")

[<AutoOpen>]
module TestCombinators =

    let dateTimeOffsetEqual (x: DateTimeOffset) (y: DateTimeOffset) =
        x.Year = y.Year
        && x.Month = y.Month
        && x.Day = y.Day
        && x.Hour = y.Hour
        && x.Minute = y.Minute
        && x.Second = y.Second
