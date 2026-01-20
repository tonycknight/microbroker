namespace microbroker.tests.acceptance

open System

module TestUtils =

    let client = new System.Net.Http.HttpClient()

    let jsonContent (json: string) =
        new Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")

[<AutoOpen>]
module TestCombinators =

    let dateTimeOffsetWithLimits (x: DateTimeOffset) (y: DateTimeOffset) =
        let delta = Math.Abs((x - y).TotalMilliseconds)

        delta < (1000.)
