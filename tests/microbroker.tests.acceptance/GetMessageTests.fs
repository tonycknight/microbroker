namespace microbroker.tests.acceptance

open System
open FsCheck.FSharp
open FsCheck.Xunit
open microbroker

[<Xunit.Collection(TestUtils.testCollection)>]
module GetMessageTests =

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

        Prop.forAll Arbitraries.invalidQueueNames property

    [<Property(MaxTest = TestUtils.maxServerTests)>]
    let ``GET Queue of unknown queue name returns empty stats`` () =
        let property queue =
            task {
                let! result = TestUtils.getQueueInfo TestUtils.host queue

                return result.name = queue && result.count = 0 && result.futureCount = 0
            }

        Prop.forAll Arbitraries.validQueueNames property

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

        Prop.forAll Arbitraries.invalidQueueNames property

    [<Property(MaxTest = TestUtils.maxServerTests)>]
    let ``GET Queue message of unknown queue name returns 404`` () =
        let property queueId =
            task {
                let uri = $"{TestUtils.host}/queues/{queueId}/message/"
                use! r = TestUtils.client.GetAsync(uri)

                return r.StatusCode = Net.HttpStatusCode.NotFound
            }

        Prop.forAll Arbitraries.validQueueNames property
