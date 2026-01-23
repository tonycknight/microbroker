namespace microbroker.tests.performance

open System
open microbroker
open NBomber
open NBomber.Contracts
open NBomber.Http.FSharp
open NBomber.FSharp

module Program =

    

    let setSimulation = 
        Scenario.withWarmUpDuration (seconds 15)
        >> Scenario.withLoadSimulations [ Inject(rate = 100, interval = seconds 1, during = seconds 180) ] 

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

            return response
        }

    let pushPullMessage httpClient messageUrl context =
        task {

            let! post = Step.run("post message", context, (pushMessage httpClient messageUrl))

            if post.IsError then
                return Response.fail ("", "Unexpected status code")
            else
                let! get = Step.run("get message", context, (pullMessage httpClient messageUrl))
                return Response.ok()
        }

    [<EntryPoint>]
    let main argv =
        
        let host = argv |> Array.tryItem 0 |> Option.defaultValue "http://localhost:8080"
        let queuesUrl = $"{host}/queues/"
        let messageUrl = $"{host}/queues/perftests/message/"

        use httpClient = Http.createDefaultClient()
        
        let pushPull = 
            Scenario.create ("push pull message", pushPullMessage httpClient messageUrl) |> setSimulation
                
        let getCounts =
            Scenario.create ("get queues", getQueues httpClient queuesUrl) |> setSimulation

        let result = 
            NBomberRunner.registerScenarios [ pushPull; getCounts ]
            |> NBomberRunner.run
        
        match result.IsOk with
        | true ->   0
        | _ ->      1
