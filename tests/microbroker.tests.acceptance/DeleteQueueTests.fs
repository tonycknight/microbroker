namespace microbroker.tests.acceptance

open System
open FsCheck.FSharp
open FsCheck.Xunit
open microbroker

[<Xunit.Collection(TestUtils.testCollection)>]
module DeleteQueueTests =

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

        Prop.forAll Arbitraries.invalidQueueNames property

    [<Property(MaxTest = 10)>]
    let ``DELETE Queue of unknown queue name returns error`` () =
        let property queueId =
            task {
                let uri = $"{TestUtils.host}/queues/{queueId}/"
                use! r = TestUtils.client.DeleteAsync(uri)

                return
                    r.StatusCode = Net.HttpStatusCode.BadRequest
                    || r.StatusCode = Net.HttpStatusCode.NotFound
            }

        Prop.forAll Arbitraries.invalidQueueNames property

    [<Property(MaxTest = 10)>]
    let ``DELETE Queue after message post`` () =
        let property (msg, queueId) =
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

        Prop.forAll (Arb.zip (Arbitraries.QueueMessages.Generate(), Arbitraries.validQueueNames)) property
