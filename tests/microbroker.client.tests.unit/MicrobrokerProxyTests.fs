namespace Microbroker.Client.Tests.Unit

open System
open Microbroker.Client
open NSubstitute
open Xunit
open FsUnit
open Microbroker.Client.Tests.Unit.TestUtils

module MicrobrokerProxyTests =

    [<Fact>]
    let ``GetQueueCounts on empty array returns empty`` () =
        task {
            let resp = notfound ""
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetQueueCounts([| "" |])

            r.Length |> should equal 0
        }

    [<Fact>]
    let ``GetQueueCounts on matching name returns value`` () =
        task {
            let name = Guid.NewGuid().ToString()

            let count =
                { MicrobrokerCount.name = name
                  count = 1
                  futureCount = 2 }

            let resp = count |> toJson |> ok
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetQueueCounts([| name |])

            r.Length |> should equal 1
            r.[0] |> should equal count
        }

    [<Fact>]
    let ``GetQueueCounts on no matching name returns empty`` () =
        task {
            let count =
                { MicrobrokerCount.name = Guid.NewGuid().ToString()
                  count = 1
                  futureCount = 2 }

            let resp = count |> toJson |> notfound
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetQueueCounts([| Guid.NewGuid().ToString() |])

            r.Length |> should equal 0
        }

    [<Fact>]
    let ``GetQueueCount on matching name returns value`` () =
        task {
            let name = Guid.NewGuid().ToString()

            let count =
                { MicrobrokerCount.name = name
                  count = 1
                  futureCount = 2 }

            let resp = count |> toJson |> ok
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetQueueCount(name)

            Option.isSome r |> should equal true
            r.Value.name |> should equal name
            r.Value.count |> should equal count.count
            r.Value.futureCount |> should equal count.futureCount
        }

    [<Fact>]
    let ``GetQueueCount on matching upper name returns value`` () =
        task {
            let name = "Aaa".ToLower()

            let count =
                { MicrobrokerCount.name = name
                  count = 1
                  futureCount = 2 }

            let resp = count |> toJson |> ok
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetQueueCount(name.ToUpper())

            r.Value.name |> should equal name
            r.Value.count |> should equal count.count
            r.Value.futureCount |> should equal count.futureCount
        }

    [<Fact>]
    let ``GetQueueCount on unknown name returning 204 returns None`` () =
        task {
            let name = "aaa"

            let count =
                { MicrobrokerCount.name = "BBB"
                  count = 1
                  futureCount = 2 }

            let resp = count |> toJson |> notfound
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetQueueCount(name)

            r |> should equal None
        }

    [<Fact>]
    let ``GetQueueCount on unknown name returning 400 returns None`` () =
        task {
            let name = "aaa"

            let count =
                { MicrobrokerCount.name = "BBB"
                  count = 1
                  futureCount = 2 }

            let resp = count |> toJson |> badRequest
            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetQueueCount(name)
                r |> should equal None

            with :? InvalidOperationException as e ->
                ignore 0
        }


    [<Fact>]
    let ``GetNext on empty returns empty`` () =
        task {
            let resp = notfound ""
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetNext("test")

            r |> should equal None
        }

    [<Fact>]
    let ``GetNext on badrequest throws exception`` () =
        task {
            let resp = badRequest ""
            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetNext("test")
                failwith "Exception not thrown"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``GetNext on notfound returns empty`` () =
        let resp = notfound ""
        let http = httpClient resp
        let proxy = defaultProxy http

        let r = proxy.GetNext("test").Result

        r |> should equal None

    [<Fact>]
    let ``GetNext returns message`` () =
        task {
            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            let resp = msg |> toJson |> ok
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetNext("test")

            r |> should equal (Some msg)
        }

    [<Fact>]
    let ``PostMany with empty sequence posts nothing`` () =
        task {
            let resp = ok "[]"
            let http = httpClientPost resp
            let proxy = defaultProxy http

            let! r = (proxy.PostMany "queue" [])

            http.DidNotReceiveWithAnyArgs().PostAsync (Arg.Any<string>()) (Arg.Any<string>())
            |> ignore
        }

    [<Fact>]
    let ``PostMany on exception does not raise exceptions`` () =
        task {
            let http = httpClientPostException ()
            let proxy = defaultProxy http

            let! r = (proxy.PostMany "queue" [])

            ignore r
        }

    [<Fact>]
    let ``PostMany with sequence`` () =
        task {
            let resp = ok "[]"
            let http = httpClientPost resp
            let proxy = defaultProxy http

            let msgs =
                [ { MicrobrokerMessage.content = "test"
                    messageType = "test msg"
                    created = DateTimeOffset.UtcNow
                    active = DateTimeOffset.UtcNow
                    expiry = DateTimeOffset.MaxValue } ]

            let! r = (proxy.PostMany "queue" msgs)

            http.ReceivedWithAnyArgs().PostAsync (Arg.Any<string>()) (toJson msgs) |> ignore
        }

    [<Fact>]
    let ``Post with value`` () =
        task {
            let resp = ok "[]"
            let http = httpClientPost resp
            let proxy = defaultProxy http

            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            let! r = (proxy.Post "queue" msg)

            http.ReceivedWithAnyArgs().PostAsync (Arg.Any<string>()) (toJson [| msg |])
            |> ignore
        }
