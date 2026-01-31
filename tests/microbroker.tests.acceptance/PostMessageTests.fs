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

    [<Property(MaxTest = TestUtils.maxServerTests)>]
    let ``POST Queue message yields on first retrival`` () =
        let property (msg, queue) =
            task {
                let uri = $"{TestUtils.host}/queues/{queue}/message/"

                let content = msg |> MessageGenerators.toJson |> TestUtils.jsonContent

                use! postResponse = TestUtils.client.PostAsync(uri, content)
                postResponse.EnsureSuccessStatusCode() |> ignore

                // Fetch from the head of the queue
                use! getResponse = TestUtils.client.GetAsync(uri)
                getResponse.EnsureSuccessStatusCode() |> ignore

                let! json = getResponse.Content.ReadAsStringAsync()
                let result = MessageGenerators.fromJson json

                let eq = dateTimeOffsetWithLimits (TimeSpan.FromSeconds 1.)

                return
                    result.messageType = msg.messageType
                    && result.content = msg.content
                    && (eq result.created msg.created)
                    && (eq result.active msg.active)
                    && (eq result.expiry msg.expiry)
            }

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate(), Arbitraries.validQueueNames)) property

    [<Property(MaxTest = TestUtils.maxServerTests)>]
    let ``POST expired queue message yields nothing`` () =
        let property (messages, queue) =
            task {
                let uri = $"{TestUtils.host}/queues/{queue}/messages/"

                let expired message =
                    { message with
                        QueueMessage.expiry = DateTimeOffset.UtcNow.AddHours -1 }

                let content = messages |> Array.map expired |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

                use! postResponse = TestUtils.client.PostAsync(uri, content)
                postResponse.EnsureSuccessStatusCode() |> ignore

                let uri = $"{TestUtils.host}/queues/{queue}/message/"
                use! getResponse = TestUtils.client.GetAsync(uri)
                return getResponse.StatusCode = Net.HttpStatusCode.NoContent
            }

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate() |> Arb.array, Arbitraries.validQueueNames)) property

    [<Property(MaxTest = TestUtils.maxServerTests)>]
    let ``POST Queue messages yields all`` () =
        let property (messages, queueId) =
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

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate() |> Arb.array, Arbitraries.validQueueNames)) property

    [<Property(MaxTest = TestUtils.maxServerTests)>]
    let ``POST Queue messages set queue stats`` () =
        let property (messages, queueId) =
            task {
                let queue = queueId.ToString()
                let uri = $"{TestUtils.host}/queues/{queue}/messages/"

                let content = messages |> MessageGenerators.toJsonArray |> TestUtils.jsonContent

                use! postResponse = TestUtils.client.PostAsync(uri, content)
                postResponse.EnsureSuccessStatusCode() |> ignore

                let! result = TestUtils.getQueueInfo TestUtils.host queue

                return result.count = messages.Length && result.name = queue && result.futureCount = 0
            }

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate() |> Arb.array, Arbitraries.validQueueNames)) property

    [<Property(MaxTest = TestUtils.maxServerTests)>]
    let ``POST Queue messages decrement queue stats`` () =
        let property (messages, queueId) =
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

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate() |> Arb.array, Arbitraries.validQueueNames)) property

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

        Prop.forAll
            (Arb.zip (Arbitraries.QueueMessages.Generate() |> Arb.array, Arbitraries.invalidQueueNames))
            property
