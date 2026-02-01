namespace microbroker.tests.acceptance

open System
open System.Linq
open System.Threading.Tasks
open FsCheck.FSharp
open FsCheck.Xunit
open Microbroker.Client

module ClientTests =

    let proxy baseUrl =
        let ihc = TestUtils.client |> InternalHttpClient :> IHttpClient

        let config =
            { MicrobrokerConfiguration.brokerBaseUrl = baseUrl
              throttleMaxTime = TimeSpan.FromSeconds 1. }

        new MicrobrokerProxy(config, ihc) :> IMicrobrokerProxy

    let getAllMessages proxy queue =
        let rec getAll (proxy: IMicrobrokerProxy) queue results =
            task {
                let! msg = proxy.GetNextAsync queue

                match msg with
                | None -> return results
                | Some msg -> return! getAll proxy queue (msg :: results)
            }

        getAll proxy queue []

    [<Property>]
    let ``GetQueueCount on invalid queue name yields Exception`` () =
        let property queueName =
            task {
                try
                    let! count = (proxy TestUtils.host).GetQueueCountAsync queueName

                    return count = None

                with :? InvalidOperationException as e ->
                    return true
            }

        Prop.forAll Arbitraries.invalidQueueNames property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``GetQueueCount on unknown queue name returns queue`` () =
        let property queueName =
            task {
                let! count = (proxy TestUtils.host).GetQueueCountAsync queueName

                return
                    count.IsSome
                    && count.Value.name = queueName
                    && count.Value.count = 0
                    && count.Value.futureCount = 0
            }

        Prop.forAll Arbitraries.validQueueNames property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``GetQueueCount on known queue name returns count`` () =
        let property (msg, queueName) =
            task {
                let proxy = proxy TestUtils.host
                do! proxy.PostAsync(queueName, msg)

                let! count = proxy.GetQueueCountAsync queueName

                return count.Value.count = 1 && count.Value.futureCount = 0
            }

        Prop.forAll (Arb.zip (Arbitraries.MicrobrokerMessages.Generate(), Arbitraries.validQueueNames)) property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``GetQueueCounts on known queue name returns counts`` () =
        let property (msgs, queueName) =
            task {
                let proxy = proxy TestUtils.host
                let queueNames = [| queueName |]
                let msgs = Array.ofSeq msgs
                do! proxy.PostManyAsync(queueName, msgs)

                let! counts = proxy.GetQueueCountsAsync queueNames

                let countNames = counts |> Seq.map _.name |> Seq.sort |> Array.ofSeq

                return
                    countNames.SequenceEqual(queueNames)
                    && counts |> Seq.map _.count |> Seq.forall (fun x -> x >= msgs.Length)
            }

        Prop.forAll
            (Arb.zip (Arbitraries.MicrobrokerMessages.Generate() |> Arb.array, Arbitraries.validQueueNames))
            property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``GetQueueCounts on unknown queue name returns empty`` () =
        let property (queueName) =
            task {
                let proxy = proxy TestUtils.host

                let! counts = proxy.GetQueueCountsAsync [| queueName |]

                return
                    counts.Length = 1
                    && counts.[0].name = queueName
                    && counts.[0].count = 0
                    && counts.[0].futureCount = 0
            }

        Prop.forAll (Arbitraries.validQueueNames) property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``GetNext on invalid queue yields Exception`` () =
        let property (queueName) =
            task {
                let proxy = proxy TestUtils.host

                try
                    let! msg = proxy.GetNextAsync queueName
                    return msg = None

                with :? InvalidOperationException as e ->
                    return true
            }

        Prop.forAll Arbitraries.invalidQueueNames property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``GetNext on unknown queue returns None`` () =
        let property (queueName) =
            task {
                let proxy = proxy TestUtils.host

                let! msg = proxy.GetNextAsync queueName

                return msg = None
            }

        Prop.forAll Arbitraries.validQueueNames property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``Post to new queue returns count and message`` () =
        let property (msg, queueName) =
            task {
                let proxy = proxy TestUtils.host

                do! proxy.PostAsync(queueName, msg)

                let! msg2 = proxy.GetNextAsync queueName

                let eq = dateTimeOffsetWithLimits (TimeSpan.FromSeconds 1.)

                return
                    msg2.Value.content = msg.content
                    && msg2.Value.messageType = msg.messageType
                    && eq msg2.Value.created msg.created
                    && eq msg2.Value.expiry msg.expiry
                    && eq msg2.Value.active msg.active
            }

        Prop.forAll (Arb.zip (Arbitraries.MicrobrokerMessages.Generate(), Arbitraries.validQueueNames)) property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``Post expiring msg to new queue returns no message`` () =
        let property (msg, queue) =
            task {
                let proxy = proxy TestUtils.host
                let expiry = TimeSpan.FromSeconds 10L

                let! _ = getAllMessages proxy queue // drain the queue

                let msg = msg |> MicrobrokerMessages.expiry (fun () -> expiry)

                do! proxy.PostAsync(queue, msg)

                do! Task.Delay(TimeSpan.FromSeconds 2L + expiry)

                let! msg = proxy.GetNextAsync queue

                return msg = None
            }

        Prop.forAll (Arb.zip (Arbitraries.MicrobrokerMessages.Generate(), Arbitraries.validQueueNames)) property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``PostMany to invalid queue yields exception`` () =
        let property (msgs, queue) =
            task {

                let msgs = msgs |> Array.ofSeq

                let proxy = proxy TestUtils.host

                try
                    do! proxy.PostManyAsync(queue, msgs)
                    return false
                with :? InvalidOperationException as e ->
                    return true
            }

        Prop.forAll
            (Arb.zip (Arbitraries.MicrobrokerMessages.Generate() |> Arb.array, Arbitraries.invalidQueueNames)
             |> Arb.filter (fun (msgs, _) -> msgs.Length > 0))
            property

    [<Property(MaxTest = TestUtils.maxClientTests)>]
    let ``PostMany to queue repeated posts are FIFO`` () =
        let property (msgs, queue) =
            task {
                let proxy = proxy TestUtils.host
                let msgs = msgs |> Array.ofSeq

                do! proxy.PostManyAsync(queue, msgs)

                let! msgs2 = getAllMessages proxy queue
                let msgs2Content = msgs2 |> Seq.rev |> Seq.map _.content |> Array.ofSeq
                let msgsContent = msgs |> Seq.map _.content |> Array.ofSeq

                return msgsContent.SequenceEqual(msgs2Content)
            }

        Prop.forAll
            (Arb.zip (Arbitraries.MicrobrokerMessages.Generate() |> Arb.array, Arbitraries.validQueueNames))
            property
