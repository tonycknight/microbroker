namespace microbroker.tests.acceptance

open System
open FsCheck.Xunit
open microbroker

module ApiTests =

    [<Property(MaxTest = 1)>]
    let ``GET Queues returns array`` () =
        task {
            let uri = "http://localhost:8080/queues/"
            use! r = TestUtils.client.GetAsync(uri)

            let _ = r.EnsureSuccessStatusCode()

            let! json = r.Content.ReadAsStringAsync()

            let result = QueueInfoGenerators.fromJsonArray json

            return
                result |> Array.length >= 0
                && result |> Array.forall (fun r -> r.name |> String.IsNullOrWhiteSpace |> not)
                && result |> Array.forall (fun r -> r.count >= 0)
                && result |> Array.forall (fun r -> r.futureCount >= 0)
        }

    [<Property(MaxTest = 10)>]
    let ``GET Queue message of unknown queue name returns empty queue`` (queueId: Guid) =
        task {
            let uri = $"http://localhost:8080/queues/{queueId}/"
            use! r = TestUtils.client.GetAsync(uri)

            let _ = r.EnsureSuccessStatusCode()

            let! json = r.Content.ReadAsStringAsync()

            let result = QueueInfoGenerators.fromJson json

            return result.name = queueId.ToString() && result.count = 0 && result.futureCount = 0
        }


    [<Property(MaxTest = 10)>]
    let ``GET Queue message of unknown queue name returns 404`` (queueId: Guid) =
        task {
            let uri = $"http://localhost:8080/queues/{queueId}/message/"
            use! r = TestUtils.client.GetAsync(uri)

            return r.StatusCode = Net.HttpStatusCode.NotFound
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |], Replay = "(5507867521403961409,11848198107970885339,50)")>]
    let ``POST Queue message yields on first retrival`` (queueId: Guid, msg: QueueMessage) =
        task {
            let uri = $"http://localhost:8080/queues/{queueId}/message/"

            let content = msg |> MessageGenerators.toJson |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            let _ = postResponse.EnsureSuccessStatusCode()

            // Fetch from the head of the queue
            use! getResponse = TestUtils.client.GetAsync(uri)
            let _ = getResponse.EnsureSuccessStatusCode()

            let! json = getResponse.Content.ReadAsStringAsync()
            let result = MessageGenerators.fromJson json

            return
                result.messageType = msg.messageType
                && result.content = msg.content
                && (dateTimeOffsetWithLimits result.created msg.created)
                && (dateTimeOffsetWithLimits result.active msg.active)
                && (dateTimeOffsetWithLimits result.expiry msg.expiry)
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST expired queue message yields nothing`` (queueId: Guid, message: QueueMessage) =
        task {
            let uri = $"http://localhost:8080/queues/{queueId}/message/"
            
            let message = { message with expiry = DateTimeOffset.UtcNow.AddMinutes -1 }
            let content = message |> MessageGenerators.toJson |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            let _ = postResponse.EnsureSuccessStatusCode()
                        
            use! getResponse = TestUtils.client.GetAsync(uri)
            return getResponse.StatusCode = Net.HttpStatusCode.NotFound
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST Queue messages yields all`` (queueId: Guid, messages: QueueMessage[]) =
        task {
            let uri = $"http://localhost:8080/queues/{queueId}/messages/"
                        
            let content = messages |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            let _ = postResponse.EnsureSuccessStatusCode()

            // Fetch all from the head of the queue until queue is drained
            let rec fetchAll (results: QueueMessage list) =
                task {
                    let uri = $"http://localhost:8080/queues/{queueId}/message/"
                    use! getResponse = TestUtils.client.GetAsync(uri)

                    if getResponse.StatusCode = Net.HttpStatusCode.NotFound then
                        return results
                    else
                        let! json = getResponse.Content.ReadAsStringAsync()
                        let message = MessageGenerators.fromJson json
                                                
                        return! fetchAll (message :: results)
                }

            let! fetchedMessages = fetchAll []
            
            // TODO: 

            return List.length fetchedMessages = messages.Length
        }
