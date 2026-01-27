namespace microbroker.tests.performance

open System
open CommandLine
open microbroker
open NBomber
open NBomber.Contracts
open NBomber.Http.FSharp
open NBomber.FSharp

module Program =

    let setWarmup duration =
        Scenario.withWarmUpDuration (seconds duration)

    let setSimulation rate duration =
        Scenario.withLoadSimulations [ Inject(rate = rate, interval = seconds 1, during = seconds duration) ]

    let genText =
        let rng = new Random()
        fun () -> 
            let size = rng.Next(100, 4000)
            let chars = Array.init size (fun _ -> char (rng.Next(32, 126)))
            String(chars)

    let getQueues httpClient queuesUrl context =
        task {

            let request = Http.createRequest "GET" queuesUrl

            let! response = request |> Http.send httpClient

            return response
        }

    let pushMessage httpClient messageUrl context =
        task {
            let now = DateTimeOffset.UtcNow

            let msg =
                { QueueMessage.content = genText ()
                  messageType = "text/plain"
                  active = now
                  created = now
                  expiry = now.AddMinutes(2.0) }

            let request = Http.createRequest "POST" messageUrl |> Http.withJsonBody msg

            let! response = request |> Http.send httpClient

            return response
        }

    let pushFutureMessage httpClient messageUrl context =
        task {
            let now = DateTimeOffset.UtcNow

            let msg =
                { QueueMessage.content = genText ()
                  messageType = "text/plain"
                  active = now.AddSeconds(15.0)
                  created = now
                  expiry = now.AddMinutes(2.0) }

            let request = Http.createRequest "POST" messageUrl |> Http.withJsonBody msg

            let! response = request |> Http.send httpClient

            return response
        }

    let pullMessage httpClient messageUrl context =
        task {
            let request = Http.createRequest "GET" messageUrl

            let! response = request |> Http.send httpClient

            return
                match response.StatusCode with
                | "OK"
                | "NotFound" -> Response.ok (sizeBytes = response.SizeBytes)
                | x -> Response.fail x
        }

    let runTests (options: CommandOptions) =
        let warmup = 15

        let queuesUrl = $"{options.host}/queues/"
        let messageUrl = $"{options.host}/queues/perftests/message/"

        use httpClient = Http.createDefaultClient ()

        let result =
            NBomberRunner.registerScenarios
                [ Scenario.create ("push message", pushMessage httpClient messageUrl)
                  |> setWarmup warmup
                  |> setSimulation options.rate options.duration

                  Scenario.create ("push future message", pushFutureMessage httpClient messageUrl)
                      |> setWarmup warmup
                      |> setSimulation options.rate options.duration

                  Scenario.create ("pull message", pullMessage httpClient messageUrl)
                      |> setWarmup warmup
                      |> setSimulation options.rate options.duration

                  Scenario.create ("get queues", getQueues httpClient queuesUrl)
                      |> setWarmup warmup
                      |> setSimulation options.rate options.duration ]
            |> NBomberRunner.withTestName "basic push/pull performance tests"
            |> NBomberRunner.withTestSuite "microbroker performance tests"
            |> NBomberRunner.run

        result

    [<EntryPoint>]
    let main argv =
        let opts = Parser.Default.ParseArguments<CommandOptions>(argv)

        match opts with
        | :? Parsed<CommandOptions> as opts ->
            printf "rate: %A duration: %A host: %s" opts.Value.rate opts.Value.duration opts.Value.host

            match (runTests opts.Value).IsOk with
            | true -> 0
            | _ -> 1
        | :? NotParsed<CommandOptions> as notParsed ->
            printf "Invalid args: %A %A" argv notParsed.Errors
            1
        | _ ->
            printf "Unknown error while parsing args: %A" argv
            1
