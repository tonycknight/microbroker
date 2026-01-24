namespace microbroker.tests.acceptance

open System
open FsCheck.FSharp
open FsCheck.Xunit
open microbroker

// TTA testing is time consuming as the time cannot be mocked out of the system. These scenarios are kept separate for XUnit parallelism.
[<Xunit.Collection(TestUtils.testCollection)>]
module PostTtaMessageTests =

    [<Property(MaxTest = 3)>]
    let ``POST Queue message with TTA shows time-delayed retrival`` () =
        let property (msg, queueId) =
            task {
                let uri = $"{TestUtils.host}/queues/{queueId}/message/"
                let delay = TimeSpan.FromSeconds 15.

                let msg =
                    { msg with
                        QueueMessage.active = (msg.created.Add delay)
                        content = $"{Guid.NewGuid().ToString()}" }

                let content = msg |> MessageGenerators.toJson |> TestUtils.jsonContent

                use! postResponse = TestUtils.client.PostAsync(uri, content)
                postResponse.EnsureSuccessStatusCode() |> ignore

                // Poll the server for signs of the messge
                let endTime = DateTime.UtcNow.Add(TimeSpan.FromMinutes 2)
                let mutable messageRetrieved = false

                while DateTime.UtcNow < endTime && (not messageRetrieved) do
                    let! queueInfo = TestUtils.getQueueInfo TestUtils.host (Strings.str queueId)

                    if queueInfo.count = 0 || queueInfo.futureCount <> 0 then
                        do! System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds 2)
                    else
                        let! retrievedMsgs = TestUtils.pullAllMessages TestUtils.host (Strings.str queueId)

                        let retrievedMsg = List.head retrievedMsgs

                        messageRetrieved <- retrievedMsg.content = msg.content

                return messageRetrieved
            }

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate(), Arbitraries.validQueueNames)) property
