namespace microbroker.tests.acceptance

open System
open System.Linq
open FsCheck.FSharp
open FsCheck.Xunit
open microbroker

module ApiTests =

    let invalidQueueNames =
        let filter (s: string) =
            s.Length > 0
            && (s.Contains('#') |> not)
            && (s.Contains('?') |> not)
            && (s.Contains('/') |> not)

        ArbMap.defaults
        |> ArbMap.arbitrary<string>
        |> Arb.filter filter
        |> Arb.filter (WebApiValidation.isValidQueueName >> not)

    [<Property(MaxTest = 1)>]
    let ``GET Queues returns array`` () =
        task {
            let! result = TestUtils.getQueueInfos TestUtils.host

            return
                result |> Array.length >= 0
                && result |> Array.forall (fun r -> r.name |> String.IsNullOrWhiteSpace |> not)
                && result |> Array.forall (fun r -> r.count >= 0)
                && result |> Array.forall (fun r -> r.futureCount >= 0)
        }

    [<Property>]
    let ``GET Queue of invalid queue name returns error`` () =
        let property queueId =
            task {
                let uri = $"{TestUtils.host}/queues/{queueId}/"
                use! r = TestUtils.client.GetAsync(uri)

                return
                    r.StatusCode = Net.HttpStatusCode.BadRequest
                    || r.StatusCode = Net.HttpStatusCode.NotFound
            }

        Prop.forAll invalidQueueNames property

    [<Property(MaxTest = 10)>]
    let ``GET Queue of unknown queue name returns empty stats`` (queueId: Guid) =
        task {
            let queue = queueId.ToString()

            let! result = TestUtils.getQueueInfo TestUtils.host queue

            return result.name = queueId.ToString() && result.count = 0 && result.futureCount = 0
        }

    [<Property>]
    let ``GET Queue message of invalid queue name returns error`` () =
        let property queueId =
            task {
                let uri = $"{TestUtils.host}/queues/{queueId}/message/"
                use! r = TestUtils.client.GetAsync(uri)

                return
                    r.StatusCode = Net.HttpStatusCode.BadRequest
                    || r.StatusCode = Net.HttpStatusCode.NotFound
            }

        Prop.forAll invalidQueueNames property

    [<Property(MaxTest = 10)>]
    let ``GET Queue message of unknown queue name returns 404`` (queueId: Guid) =
        task {
            let uri = $"{TestUtils.host}/queues/{queueId}/message/"
            use! r = TestUtils.client.GetAsync(uri)

            return r.StatusCode = Net.HttpStatusCode.NotFound
        }

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

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate(), invalidQueueNames)) property


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

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate() |> Arb.array, invalidQueueNames)) property

    [<Property>]
    let ``DELETE Queue of invalid queue name returns error`` () =
        let property queueId =
            task {
                let uri = $"{TestUtils.host}/queues/{queueId}/"
                use! r = TestUtils.client.DeleteAsync(uri)

                return
                    r.StatusCode = Net.HttpStatusCode.BadRequest
                    || r.StatusCode = Net.HttpStatusCode.NotFound
            }

        Prop.forAll invalidQueueNames property

    [<Property(MaxTest = 10)>]
    let ``DELETE Queue of unknown queue name returns error`` (queueId: Guid) =
        task {
            let uri = $"{TestUtils.host}/queues/{queueId}/"
            use! r = TestUtils.client.DeleteAsync(uri)

            return
                r.StatusCode = Net.HttpStatusCode.BadRequest
                || r.StatusCode = Net.HttpStatusCode.NotFound
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.QueueMessages> |])>]
    let ``DELETE Queue after message post`` (queueId: Guid, msg: QueueMessage) =
        task {
            let uri = $"{TestUtils.host}/queues/{queueId}/message/"

            let content = msg |> MessageGenerators.toJson |> TestUtils.jsonContent

            use! postResponse = TestUtils.client.PostAsync(uri, content)
            postResponse.EnsureSuccessStatusCode() |> ignore

            let uri = $"{TestUtils.host}/queues/{queueId}/"
            use! deleteResponse = TestUtils.client.DeleteAsync(uri)
            deleteResponse.EnsureSuccessStatusCode() |> ignore

            let! queueInfo = TestUtils.getQueueInfo TestUtils.host (queueId.ToString())

            return queueInfo.count = 0
        }
