namespace Microbroker.Client.Tests.Integration

open System
open Microbroker.Client
open FsUnit
open Xunit

module Tests =
    let proxy () =
        Factories.apiBaseUrl () |> Factories.proxy

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
            let mbp = proxy ()

            let! count = Factories.queueName () |> mbp.GetQueueCount

            count |> should equal None
        }

    [<Fact>]
    let ``GetQueueCount on known queue name returns count`` () =
        task {
            let proxy = proxy ()
            let queueName = Factories.queueName ()
            let msg = Factories.msg ()
            do! proxy.Post queueName msg

            let! count = proxy.GetQueueCount queueName

            let! _ = getAllMessages proxy queueName // drain the queue

            count.Value.count |> should equal 1
            count.Value.futureCount |> should equal 0
        }

    [<Fact>]
    let ``GetQueueCounts on known queue name returns counts`` () =
        task {
            let proxy = proxy ()
            let msg = Factories.msg ()

            let queueNames = [| 1..3 |] |> Array.map (fun _ -> Factories.queueName ())

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
            let proxy = proxy ()
            let msg = Factories.msg ()

            let queueNames = [| 1..3 |] |> Array.map (fun _ -> Factories.queueName ())

            let! counts = proxy.GetQueueCounts queueNames

            counts.Length |> should equal 0
        }


    [<Fact>]
    let ``Post to new queue returns count and message`` () =
        task {
            let proxy = proxy ()
            let queue = Factories.queueName ()

            let! count = proxy.GetQueueCount queue
            count |> should equal None

            let msg = Factories.msg ()

            do! proxy.Post queue msg

            let! msg2 = proxy.GetNext queue

            msg2.Value.content |> should equal msg.content
            msg2.Value.messageType |> should equal msg.messageType
        }

    [<Fact>]
    let ``Post expiring msg to new queue returns count and no message`` () =
        task {
            let proxy = proxy ()
            let queue = Factories.queueName ()
            let expiry = TimeSpan.FromSeconds 5

            let! count = proxy.GetQueueCount queue
            count |> should equal None

            let msg = Factories.msg () |> MicrobrokerMessages.expiry (fun () -> expiry)

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
            let proxy = proxy ()
            let queue = Factories.queueName ()

            let msgs = [| 1..3 |] |> Array.map (fun _ -> Factories.msg ())

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
