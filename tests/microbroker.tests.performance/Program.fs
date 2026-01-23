namespace microbroker.tests.performance

open System
open microbroker
open NBomber
open NBomber.Contracts
open NBomber.Http.FSharp
open NBomber.FSharp

module Program =

    let queuesUrl = "http://localhost:8080/queues/"
    let messageUrl = "http://localhost:8080/queues/perftests/message/"

    let setSimulation = 
        Scenario.withWarmUpDuration (seconds 15)
        >> Scenario.withLoadSimulations [ Inject(rate = 100, interval = seconds 1, during = seconds 30) ] 

    let getQueues httpClient context =
        task {
                    
            let request = Http.createRequest "GET" queuesUrl
                    
            let! response = request |> Http.send httpClient

            return response
        }

    let pushMessage httpClient context =
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

    let pullMessage httpClient context = 
        task {
            let request = Http.createRequest "GET" messageUrl
                
            let! response = request |> Http.send httpClient

            return response
        }

    let pushPullMessage httpClient context =
        task {

            let! post = Step.run("post message", context, (pushMessage httpClient))

            if post.IsError then
                return Response.fail ("", "Unexpected status code")
            else
                let! get = Step.run("get message", context, (pullMessage httpClient))
                return Response.ok()
        }

    [<EntryPoint>]
    let main argv =
        
        use httpClient = Http.createDefaultClient()
        
        let pushPull = 
            Scenario.create ("push pull message", pushPullMessage httpClient) |> setSimulation
                
        let getCounts =
            Scenario.create ("get queues", getQueues httpClient) |> setSimulation

        NBomberRunner.registerScenarios [ pushPull; getCounts ]
        |> NBomberRunner.run |> ignore
        
        0
