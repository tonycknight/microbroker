namespace microbroker.tests.performance

open System
open microbroker
open NBomber
open NBomber.Contracts
open NBomber.Http.FSharp
open NBomber.FSharp

module Program =

    let setSimulation rate duration = 
        Scenario.withWarmUpDuration (seconds 15)
        >> Scenario.withLoadSimulations [ Inject(rate = rate, interval = seconds 1, during = seconds duration) ] 

    let getQueues httpClient queuesUrl context =
        task {
                    
            let request = Http.createRequest "GET" queuesUrl
                    
            let! response = request |> Http.send httpClient

            return response
        }

    let pushMessage httpClient messageUrl context =
        task {
            let now = DateTimeOffset.UtcNow

            let msg = { QueueMessage.content = $"Message {Guid.NewGuid()}"
                        messageType = "text/plain" 
                        active = now
                        created = now
                        expiry = now.AddMinutes(10.0) }

            let request = 
                Http.createRequest "POST" messageUrl
                |> Http.withJsonBody msg
                                    
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

    [<EntryPoint>]
    let main argv =
        
        let host = argv |> Array.tryItem 0 |> Option.defaultValue "http://localhost:8080"
        let duration = 180
        let rate = 100

        let queuesUrl = $"{host}/queues/"
        let messageUrl = $"{host}/queues/perftests/message/"

        use httpClient = Http.createDefaultClient()
        
        let result = 
            NBomberRunner.registerScenarios [ 
                Scenario.create ("push message", pushMessage httpClient messageUrl) |> setSimulation rate duration; 
                Scenario.create ("pull message", pullMessage httpClient messageUrl) |> setSimulation rate duration; 
                Scenario.create ("get queues", getQueues httpClient queuesUrl) |> setSimulation rate duration
            ]
            |> NBomberRunner.run
        
        match result.IsOk with
        | true ->   0
        | _ ->      1
