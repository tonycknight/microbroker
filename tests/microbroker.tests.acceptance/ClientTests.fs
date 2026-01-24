namespace microbroker.tests.acceptance

open System
open System.Linq
open FsCheck.FSharp
open FsCheck.Xunit
open FsUnit
open Microsoft.Extensions.Logging
open Microbroker.Client

module ClientTests =
    [<Literal>]
    let maxTests = 20

    let proxy baseUrl =
        let ihc = TestUtils.client |> InternalHttpClient :> IHttpClient
        let config = 
            { MicrobrokerConfiguration.brokerBaseUrl = baseUrl
              throttleMaxTime = TimeSpan.FromSeconds 1. }
        let log = NSubstitute.Substitute.For<ILoggerFactory>()
        new MicrobrokerProxy(config, ihc, log) :> IMicrobrokerProxy

    let getAllMessages proxy queue =
        let rec getAll (proxy: IMicrobrokerProxy) queue results =
            task {
                let! msg = proxy.GetNext queue

                match msg with
                | None -> return results
                | Some msg -> return! getAll proxy queue (msg :: results)
            }

        getAll proxy queue []

    [<Property(MaxTest = maxTests)>]
    let ``GetQueueCount on unknown queue name returns None`` () =
        let property queueName =
            task {
                let! count = queueName.ToString() |> (proxy TestUtils.host).GetQueueCount

                return count = None
            }

        Prop.forAll Arbitraries.validQueueNames property

    [<Property(MaxTest = maxTests)>]
    let ``GetQueueCount on known queue name returns count`` () =
        let property (msg, queueName) =
            task {
                let proxy = proxy TestUtils.host
                do! proxy.Post queueName msg

                let! count = proxy.GetQueueCount queueName

                return count.Value.count = 1
                        && count.Value.futureCount = 0
            }

        Prop.forAll (Arb.zip (Arbitraries.MicrobrokerMessages.Generate(), Arbitraries.validQueueNames)) property

    [<Property(MaxTest = maxTests)>]
    let ``GetQueueCounts on known queue name returns counts`` () =
        let property (msgs, queueName) =
            task {
                let proxy = proxy TestUtils.host
                let queueNames = [| queueName |]
                let msgs = Array.ofSeq msgs
                do! msgs |> proxy.PostMany queueName
                
                let! counts = proxy.GetQueueCounts queueNames

                let countNames = counts |> Seq.map _.name |> Seq.sort |> Array.ofSeq
                
                return countNames.SequenceEqual(queueNames)
                        && counts |> Seq.map _.count |> Seq.forall (fun x -> x = msgs.Length)
            }

        Prop.forAll (Arb.zip (Arbitraries.MicrobrokerMessages.Generate() |> Arb.array, Arbitraries.validQueueNames)) property

    [<Property(MaxTest = maxTests)>]
    let ``GetQueueCounts on unknown queue name returns empty`` () =
        let property (queueName) =
            task {
                let proxy = proxy TestUtils.host
                
                let! counts = proxy.GetQueueCounts [| queueName |]

                return counts.Length = 0
            }

        Prop.forAll (Arbitraries.validQueueNames) property

    [<Property(MaxTest = maxTests)>]
    let ``Post to new queue returns count and message`` () =
        let property (msg, queueName) =
            task {
                let proxy = proxy TestUtils.host
                
                let! count = proxy.GetQueueCount queueName
                count |> should equal None

                do! proxy.Post queueName msg

                let! msg2 = proxy.GetNext queueName

                let eq = dateTimeOffsetWithLimits (TimeSpan.FromSeconds 1.)

                return 
                    msg2.Value.content = msg.content
                    && msg2.Value.messageType = msg.messageType
                    && eq msg2.Value.created msg.created
                    && eq msg2.Value.expiry msg.expiry
                    && eq msg2.Value.active msg.active
            }

        Prop.forAll (Arb.zip (Arbitraries.MicrobrokerMessages.Generate(), Arbitraries.validQueueNames)) property

    [<Property(MaxTest = 10)>]
    let ``Post expiring msg to new queue returns count and no message`` () =
        let property (msg, queue) =
            task {
                let proxy = proxy TestUtils.host
                let expiry = TimeSpan.FromSeconds 5

                let! count = proxy.GetQueueCount queue
                count |> should equal None

                let msg = msg |> MicrobrokerMessages.expiry (fun () -> expiry)

                do! proxy.Post queue msg

                let! count = proxy.GetQueueCount queue
                count.Value.count |> should equal 1

                do! System.Threading.Tasks.Task.Delay(expiry.Add(TimeSpan.FromSeconds 2))

                let! msg2 = proxy.GetNext queue

                return msg2 = None
            }

        Prop.forAll (Arb.zip (Arbitraries.MicrobrokerMessages.Generate(), Arbitraries.validQueueNames)) property

    [<Property(MaxTest = maxTests)>]
    let ``PostMany to queue repeated posts are FIFO`` () =
        let property (msgs, queue) = 
            task {
                let proxy = proxy TestUtils.host
                let msgs = msgs |> Array.ofSeq

                do! proxy.PostMany queue msgs

                let! msgs2 = getAllMessages proxy queue

                (msgs2 |> Seq.rev |> Seq.map _.content)
                |> should equal (msgs |> Seq.map _.content)

                let! count = proxy.GetQueueCount queue
                return 
                    count.Value.count = 0
                    && count.Value.futureCount = 0
            }

        Prop.forAll (Arb.zip (Arbitraries.MicrobrokerMessages.Generate() |> Arb.array, Arbitraries.validQueueNames)) property
