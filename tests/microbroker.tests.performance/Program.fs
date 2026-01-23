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
        Scenario.withWarmUpDuration (seconds 10)
        >> Scenario.withLoadSimulations [ Inject(rate = 1000, interval = seconds 1, during = seconds 30) ] 



    [<EntryPoint>]
    let main argv =
        
        use httpClient = Http.createDefaultClient()

        let getQueues context =
            task {
                    
                let request = Http.createRequest "GET" queuesUrl
                    
                let! response = request |> Http.send httpClient

                return match response.IsError with
                        | false ->   Response.ok()
                        | _ ->      Response.fail (response.StatusCode, "Unexpected status code")                                                
            }

        let pushMessage context =
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

        let pullMessage context = 
            task {
                let request = Http.createRequest "GET" messageUrl
                
                let! response = request |> Http.send httpClient

                return response
            }

        let pushPullMessage context =
            task {

                let! post = Step.run("post message", context, pushMessage)

                if post.IsError then
                    return Response.fail ("", "Unexpected status code")
                else
                    let! get = Step.run("get message", context, pullMessage)                    
                    return Response.ok()
            }
        
        let pushPull = 
            Scenario.create ("push pull message", pushPullMessage) |> setSimulation
                
        let getCounts =
            Scenario.create ("get queues", getQueues) |> setSimulation

        NBomberRunner.registerScenarios [ pushPull; getCounts ]
        |> NBomberRunner.run |> ignore
        
        0
