namespace microbroker.tests.acceptance

open System
open System.Linq
open FsCheck.Xunit
open microbroker

module ApiTests =

    [<Literal>]
    let host = "http://localhost:8080"

    [<Property(MaxTest = 1)>]
    let ``GET Queues returns array`` () =
        task {
            let! result = TestUtils.getQueueInfos host

            return
                result |> Array.length >= 0
                && result |> Array.forall (fun r -> r.name |> String.IsNullOrWhiteSpace |> not)
                && result |> Array.forall (fun r -> r.count >= 0)
                && result |> Array.forall (fun r -> r.futureCount >= 0)
        }

    [<Property(MaxTest = 10)>]
    let ``GET Queue message of unknown queue name returns empty queue`` (queueId: Guid) =
        task {
            let queue = queueId.ToString()

            let! result = TestUtils.getQueueInfo host queue

            return result.name = queueId.ToString() && result.count = 0 && result.futureCount = 0
        }

    [<Property(MaxTest = 10)>]
    let ``GET Queue message of unknown queue name returns 404`` (queueId: Guid) =
        task {
            let uri = $"{host}/queues/{queueId}/message/"
            use! r = TestUtils.client.GetAsync(uri)

            return r.StatusCode = Net.HttpStatusCode.NotFound
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST Queue message yields on first retrival`` (queueId: Guid, msg: QueueMessage) =
        task {
            let uri = $"{host}/queues/{queueId}/message/"

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
            let uri = $"{host}/queues/{queueId}/message/"

            let message =
                { message with
                    expiry = DateTimeOffset.UtcNow.AddMinutes -1 }

            let content = message |> MessageGenerators.toJson |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            let _ = postResponse.EnsureSuccessStatusCode()

            use! getResponse = TestUtils.client.GetAsync(uri)
            return getResponse.StatusCode = Net.HttpStatusCode.NotFound
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST Queue messages yields all`` (queueId: Guid, messages: QueueMessage[]) =

        task {
            let queue = queueId.ToString()
            let uri = $"{host}/queues/{queue}/messages/"

            let content = messages |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            let _ = postResponse.EnsureSuccessStatusCode()

            let! fetchedMessages = TestUtils.pullAllMessages host queue

            let fetchedPairs =
                fetchedMessages |> Seq.map (fun m -> (m.messageType, m.content)) |> Seq.sort

            let originalPairs =
                messages |> Seq.map (fun m -> (m.messageType, m.content)) |> Seq.sort

            return
                List.length fetchedMessages = messages.Length
                && originalPairs.SequenceEqual(fetchedPairs)
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST Queue messages set queue stats`` (queueId: Guid, messages: QueueMessage[]) =

        task {
            let queue = queueId.ToString()
            let uri = $"{host}/queues/{queue}/messages/"

            let content = messages |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            let _ = postResponse.EnsureSuccessStatusCode()

            let! result = TestUtils.getQueueInfo host queue

            return result.count = messages.Length && result.name = queue && result.futureCount = 0
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST Queue messages decrement queue stats`` (queueId: Guid, messages: QueueMessage[]) =

        task {
            let queue = queueId.ToString()
            let uri = $"{host}/queues/{queue}/messages/"

            let content = messages |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            let _ = postResponse.EnsureSuccessStatusCode()

            let! drainResults = TestUtils.pullAllQueueInfos host queue

            let counts = drainResults |> List.map _.count
            let expected = [ 0 .. messages.Length ] |> List.map int64

            return
                List.length counts = messages.Length + 1
                // Verify that each count matches
                && (counts |> List.zip expected |> List.map (fun (x, y) -> x = y) |> List.forall id)
        }
