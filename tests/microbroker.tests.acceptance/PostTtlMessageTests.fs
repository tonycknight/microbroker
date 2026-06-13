namespace microbroker.tests.acceptance

open System
open FsCheck.FSharp
open FsCheck.Xunit
open microbroker

// TTL testing is time consuming as the time cannot be mocked out of the system. These scenarios are kept separate for XUnit parallelism.
module PostTtlMessageTests =

    [<Property(MaxTest = 2)>]
    let ``GET from unknown queue with TTL shows time-delayed retrieval`` () =
        let property (msg, queueId) =
            task {
                let uri = $"{TestUtils.host}/queues/{queueId}/message/?ttl=5"

                // Poll the server for signs of the messge
                let endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds 30L)
                let mutable messageRetrieved = false

                while DateTime.UtcNow < endTime && (not messageRetrieved) do
                    use! postResponse = TestUtils.client.GetAsync(uri)

                    messageRetrieved <- postResponse.StatusCode <> Net.HttpStatusCode.NoContent

                    if not messageRetrieved then
                        do! System.Threading.Tasks.Task.Delay(500)

                return not messageRetrieved
            }

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate(), Arbitraries.validQueueNames)) property

    [<Property(MaxTest = 2)>]
    let ``POST Queue message with TTL shows time-delayed retrieval`` () =
        let property (msg, queueId) =
            task {
                let uri = $"{TestUtils.host}/queues/{queueId}/message/?ttl=5"

                // Post a message that immediate expires
                let msg =
                    { msg with
                        QueueMessage.content = $"{Guid.NewGuid().ToString()}"
                        expiry = DateTimeOffset.UtcNow.AddDays(-1) }

                let content = msg |> MessageGenerators.toJson |> TestUtils.jsonContent

                use! postResponse = TestUtils.client.PostAsync(uri, content)
                postResponse.EnsureSuccessStatusCode() |> ignore

                // Poll the server for signs of the messge
                let endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds 30L)
                let mutable messageRetrieved = false

                while DateTime.UtcNow < endTime && (not messageRetrieved) do
                    use! postResponse = TestUtils.client.GetAsync(uri)

                    messageRetrieved <- postResponse.StatusCode <> Net.HttpStatusCode.NoContent

                    if not messageRetrieved then
                        do! System.Threading.Tasks.Task.Delay(500)

                return not messageRetrieved
            }

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate(), Arbitraries.validQueueNames)) property
