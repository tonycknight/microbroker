namespace microbroker.tests.acceptance

open System
open FsUnit
open Microsoft.Extensions.Logging
open Microbroker.Client
open Xunit

module ClientTests =
    let log () =
        NSubstitute.Substitute.For<ILoggerFactory>()

    let config url =
        { MicrobrokerConfiguration.brokerBaseUrl = url
          throttleMaxTime = TimeSpan.FromSeconds 1. }

    let proxy baseUrl =
        let ihc = TestUtils.client |> InternalHttpClient :> IHttpClient
        let config = config baseUrl
        let log = log ()
        new MicrobrokerProxy(config, ihc, log) :> IMicrobrokerProxy

    let queueName () =
        $"integration_test_queue_{Guid.NewGuid().ToString()}"

    let msg () =
        MicrobrokerMessages.create ()
        |> MicrobrokerMessages.content $"here I am {Guid.NewGuid().ToString()}"
        |> MicrobrokerMessages.messageType $"message type {Guid.NewGuid().ToString()}"


    let getAllMessages proxy queue =
        let rec getAll (proxy: IMicrobrokerProxy) queue results =
            task {
                let! msg = proxy.GetNext queue

                match msg with
                | None -> return results
                | Some msg -> return! getAll proxy queue (msg :: results)
            }

        getAll proxy queue []

    let postAllQueues (proxy: IMicrobrokerProxy) queues msg =
        task {
            let posts = queues |> Array.map (fun q -> proxy.Post q msg)

            let! r = System.Threading.Tasks.Task.WhenAll posts

            ignore r
        }

    let getFromAllQueues (proxy: IMicrobrokerProxy) queues =
        task {
            let gets = queues |> Array.map (fun q -> getAllMessages proxy q)

            let! r = System.Threading.Tasks.Task.WhenAll gets

            return r |> Seq.collect id |> List.ofSeq
        }


    [<Fact>]
    let ``GetQueueCount on unknown queue name returns None`` () =
        task {            
            let! count = queueName () |> (proxy TestUtils.host).GetQueueCount

            count |> should equal None
        }

    [<Fact>]
    let ``GetQueueCount on known queue name returns count`` () =
        task {
            let proxy = proxy TestUtils.host
            let queueName = queueName ()
            let msg = msg ()
            do! proxy.Post queueName msg

            let! count = proxy.GetQueueCount queueName

            let! _ = getAllMessages proxy queueName // drain the queue

            count.Value.count |> should equal 1
            count.Value.futureCount |> should equal 0
        }

    [<Fact>]
    let ``GetQueueCounts on known queue name returns counts`` () =
        task {
            let proxy = proxy TestUtils.host
            let msg = msg ()

            let queueNames = [| 1..3 |] |> Array.map (fun _ -> queueName ())

            let posts = queueNames |> Array.map (fun q -> proxy.Post q msg)

            let! r = System.Threading.Tasks.Task.WhenAll posts

            let! counts = proxy.GetQueueCounts queueNames

            let! _ = getFromAllQueues proxy queueNames // drain the queue

            (counts |> Seq.map _.name |> Seq.sort) |> should equal (queueNames |> Seq.sort)
            (counts |> Seq.map _.count) |> should equal (queueNames |> Seq.map (fun _ -> 1))
        }


    [<Fact>]
    let ``GetQueueCounts on unknown queue name returns empty`` () =
        task {
            let proxy = proxy TestUtils.host
            let msg = msg ()

            let queueNames = [| 1..3 |] |> Array.map (fun _ -> queueName ())

            let! counts = proxy.GetQueueCounts queueNames

            counts.Length |> should equal 0
        }


    [<Fact>]
    let ``Post to new queue returns count and message`` () =
        task {
            let proxy = proxy TestUtils.host
            let queue = queueName ()

            let! count = proxy.GetQueueCount queue
            count |> should equal None

            let msg = msg ()

            do! proxy.Post queue msg

            let! msg2 = proxy.GetNext queue

            msg2.Value.content |> should equal msg.content
            msg2.Value.messageType |> should equal msg.messageType
        }

    [<Fact>]
    let ``Post expiring msg to new queue returns count and no message`` () =
        task {
            let proxy = proxy TestUtils.host
            let queue = queueName ()
            let expiry = TimeSpan.FromSeconds 5

            let! count = proxy.GetQueueCount queue
            count |> should equal None

            let msg = msg () |> MicrobrokerMessages.expiry (fun () -> expiry)

            do! proxy.Post queue msg

            let! count = proxy.GetQueueCount queue
            count.Value.count |> should equal 1

            do! System.Threading.Tasks.Task.Delay(expiry.Add(TimeSpan.FromSeconds 2))

            let! msg2 = proxy.GetNext queue

            msg2 |> should equal None
        }

    [<Fact>]
    let ``PostMany to queue repeated posts are FIFO`` () =
        task {
            let proxy = proxy TestUtils.host
            let queue = queueName ()

            let msgs = [| 1..3 |] |> Array.map (fun _ -> msg ())

            do! proxy.PostMany queue msgs

            let! count = proxy.GetQueueCount queue
            count.Value.count |> should equal msgs.Length
            count.Value.futureCount |> should equal 0

            let! msgs2 = getAllMessages proxy queue

            (msgs2 |> Seq.rev |> Seq.map _.content)
            |> should equal (msgs |> Seq.map _.content)

            let! count = proxy.GetQueueCount queue
            count.Value.count |> should equal 0
            count.Value.futureCount |> should equal 0
        }
