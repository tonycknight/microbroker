namespace microbroker.tests.acceptance

open System
open FsCheck.Xunit
open microbroker

module ApiTests =

    [<Property(MaxTest = 1)>]
    let ``GET Queues returns array`` () =
        task {
            let uri = "http://localhost:8080/queues/"
            let! r = TestUtils.client.GetAsync(uri)

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
            let! r = TestUtils.client.GetAsync(uri)

            let _ = r.EnsureSuccessStatusCode()

            let! json = r.Content.ReadAsStringAsync()

            let result = QueueInfoGenerators.fromJson json

            return result.name = queueId.ToString() && result.count = 0 && result.futureCount = 0
        }


    [<Property(MaxTest = 10)>]
    let ``GET Queue message of unknown queue name returns 404`` (queueId: Guid) =
        task {
            let uri = $"http://localhost:8080/queues/{queueId}/message/"
            let! r = TestUtils.client.GetAsync(uri)

            return r.StatusCode = Net.HttpStatusCode.NotFound
        }

    [<Property(MaxTest = 10, Arbitrary = [| typeof<Arbitraries.AlphaNumericString> |])>]
    let ``POST Queue message yields on first retrival`` (queueId: Guid, content: string, messageType: string) =
        task {
            let uri = $"http://localhost:8080/queues/{queueId}/message/"

            let msg = MessageGenerators.message messageType content

            let content = msg |> MessageGenerators.toJson |> TestUtils.jsonContent

            let! postResponse = TestUtils.client.PostAsync(uri, content)
            let _ = postResponse.EnsureSuccessStatusCode()

            // Fetch from the head of the queue
            let! getResponse = TestUtils.client.GetAsync(uri)
            let _ = getResponse.EnsureSuccessStatusCode()

            let! json = getResponse.Content.ReadAsStringAsync()
            let result = MessageGenerators.fromJson json

            return
                result.messageType = msg.messageType
                && result.content = msg.content
                && (dateTimeOffsetEqual result.created msg.created)
                && (dateTimeOffsetEqual result.active msg.active)
                && (dateTimeOffsetEqual result.expiry msg.expiry)
        }
