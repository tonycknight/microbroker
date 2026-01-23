namespace microbroker.tests.acceptance

open System
open System.Linq
open FsCheck.FSharp
open FsCheck.Xunit
open microbroker

module PostMessageTests =
    [<Property>]
    let ``POST Queue message to invalid queue yields error`` () =
        let property (msg, name) =
            task {
                let uri = $"{TestUtils.host}/queues/{name}/message/"

                let content = msg |> MessageGenerators.toJson |> TestUtils.jsonContent

                use! r = TestUtils.client.PostAsync(uri, content)

                return
                    r.StatusCode = Net.HttpStatusCode.BadRequest
                    || r.StatusCode = Net.HttpStatusCode.NotFound
            }

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate(), Arbitraries.invalidQueueNames)) property


    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST Queue message yields on first retrival`` (queueId: Guid, msg: QueueMessage) =
        task {
            let uri = $"{TestUtils.host}/queues/{queueId}/message/"

            let content = msg |> MessageGenerators.toJson |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            postResponse.EnsureSuccessStatusCode() |> ignore

            // Fetch from the head of the queue
            use! getResponse = TestUtils.client.GetAsync(uri)
            getResponse.EnsureSuccessStatusCode() |> ignore

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
            let uri = $"{TestUtils.host}/queues/{queueId}/message/"

            let message =
                { message with
                    expiry = DateTimeOffset.UtcNow.AddMinutes -1 }

            let content = message |> MessageGenerators.toJson |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            postResponse.EnsureSuccessStatusCode() |> ignore

            use! getResponse = TestUtils.client.GetAsync(uri)
            return getResponse.StatusCode = Net.HttpStatusCode.NotFound
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST Queue messages yields all`` (queueId: Guid, messages: QueueMessage[]) =

        task {
            let queue = queueId.ToString()
            let uri = $"{TestUtils.host}/queues/{queue}/messages/"

            let content = messages |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            postResponse.EnsureSuccessStatusCode() |> ignore

            let! fetchedMessages = TestUtils.pullAllMessages TestUtils.host queue

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
            let uri = $"{TestUtils.host}/queues/{queue}/messages/"

            let content = messages |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            postResponse.EnsureSuccessStatusCode() |> ignore

            let! result = TestUtils.getQueueInfo TestUtils.host queue

            return result.count = messages.Length && result.name = queue && result.futureCount = 0
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``POST Queue messages decrement queue stats`` (queueId: Guid, messages: QueueMessage[]) =

        task {
            let queue = queueId.ToString()
            let uri = $"{TestUtils.host}/queues/{queue}/messages/"

            let content = messages |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            postResponse.EnsureSuccessStatusCode() |> ignore

            let! drainResults = TestUtils.pullAllQueueInfos TestUtils.host queue

            let counts = drainResults |> List.map _.count
            let expected = [ 0 .. messages.Length ] |> List.map int64

            return
                List.length counts = messages.Length + 1
                // Verify that each count matches
                && (counts |> List.zip expected |> List.map (fun (x, y) -> x = y) |> List.forall id)
        }

    [<Property>]
    let ``POST Queue messages to invalid queue yields error`` () =
        let property (msgs, name) =
            task {
                let uri = $"{TestUtils.host}/queues/{name}/messages/"

                let content = msgs |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

                use! r = TestUtils.client.PostAsync(uri, content)

                return
                    r.StatusCode = Net.HttpStatusCode.BadRequest
                    || r.StatusCode = Net.HttpStatusCode.NotFound
            }

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate() |> Arb.array, Arbitraries.invalidQueueNames)) property

    