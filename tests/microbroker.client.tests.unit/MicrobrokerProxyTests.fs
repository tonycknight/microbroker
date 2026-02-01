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
    let ``GetQueueCount on bad request throws exception`` () =
        task {
            let name = "aaa"

            let count =
                { MicrobrokerCount.name = "BBB"
                  count = 1
                  futureCount = 2 }

            let json = toJson count
            let errors = [| "test error" |]
            let resp = badRequestErrors json errors

            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetQueueCount(name)
                r |> should equal None

            with :? InvalidOperationException as e when e.Message = errors.[0] ->
                ignore e
        }

    [<Fact>]
    let ``GetQueueCounts on bad gateway throws exception`` () =
        task {
            let name = "aaa"

            let resp = badGatewayResponse ()
            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetQueueCount(name)
                failwith "No exception thrown"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``GetQueueCounts on too many requests throws exception`` () =
        task {
            let name = "aaa"

            let resp = tooManyRequestsResponse ()
            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetQueueCount(name)
                failwith "No exception thrown"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``GetQueueCounts on exception throws exception`` () =
        task {
            let name = "aaa"
            let ex = new ArgumentNullException()
            let resp = exceptionResponse ex
            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetQueueCount(name)
                r |> should equal None

            with :? ArgumentNullException as e ->
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
        task {
            let resp = notfound ""
            let http = httpClient resp
            let proxy = defaultProxy http

            let! r = proxy.GetNext("test")

            r |> should equal None
        }

    [<Fact>]
    let ``GetNext on bad gateway throws exception`` () =
        task {
            let name = "aaa"

            let resp = badGatewayResponse ()
            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetNext(name)
                failwith "No exception thrown"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``GetNext on too many requests throws exception`` () =
        task {
            let name = "aaa"

            let resp = tooManyRequestsResponse ()
            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetNext(name)
                failwith "No exception thrown"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``GetNext on exception throws exception`` () =
        task {
            let name = "aaa"
            let ex = new ArgumentNullException()
            let resp = exceptionResponse ex
            let http = httpClient resp
            let proxy = defaultProxy http

            try
                let! r = proxy.GetNext(name)
                failwith "No exception thrown"

            with :? ArgumentNullException as e ->
                ignore 0
        }

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

            let! r = proxy.PostMany "queue" []

            http.DidNotReceiveWithAnyArgs().PostAsync (Arg.Any<string>()) (Arg.Any<string>())
            |> ignore
        }

    [<Fact>]
    let ``PostMany on exception raises exceptions`` () =
        task {
            let ex = new ArgumentException("test message please ignore")


            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            let resp = exceptionResponse ex
            let http = httpClientPost resp
            let proxy = defaultProxy http

            try
                let! r = proxy.PostMany "queue" [| msg |]
                failwith "Exception not thrown"

            with :? ArgumentException as e when e.Message = ex.Message ->
                ignore 0
        }

    [<Fact>]
    let ``PostMany on bad request raises exceptions`` () =
        task {
            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            let resp = badRequest ""

            let http = httpClientPost resp
            let proxy = defaultProxy http

            try
                let! r = proxy.PostMany "queue" [| msg |]
                failwith "Exception not thrown"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``PostMany on bad gateway raises exceptions`` () =
        task {
            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            let resp = badGatewayResponse ()

            let http = httpClientPost resp
            let proxy = defaultProxy http

            try
                let! r = proxy.PostMany "queue" [| msg |]
                failwith "Exception not thrown"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``PostMany on too many request raises exceptions`` () =
        task {
            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            let resp = tooManyRequestsResponse ()

            let http = httpClientPost resp
            let proxy = defaultProxy http

            try
                let! r = proxy.PostMany "queue" [| msg |]
                failwith "Exception not thrown"

            with :? InvalidOperationException as e ->
                ignore 0
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

            let! r = proxy.PostMany "queue" msgs

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

            let! r = proxy.Post "queue" msg

            http.ReceivedWithAnyArgs().PostAsync (Arg.Any<string>()) (toJson [| msg |])
            |> ignore
        }

    [<Fact>]
    let ``Post on bad request throws exception`` () =
        task {
            let resp = badRequest ""
            let http = httpClientPost resp
            let proxy = defaultProxy http

            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            try
                let! r = proxy.Post "queue" msg
                failwith "Exception not raised"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``Post on bad gateway throws exception`` () =
        task {
            let resp = badGatewayResponse ()
            let http = httpClientPost resp
            let proxy = defaultProxy http

            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            try
                let! r = proxy.Post "queue" msg
                failwith "Exception not raised"

            with :? InvalidOperationException as e ->
                ignore 0
        }

    [<Fact>]
    let ``Post on too many requests throws exception`` () =
        task {
            let resp = tooManyRequestsResponse ()
            let http = httpClientPost resp
            let proxy = defaultProxy http

            let msg =
                { MicrobrokerMessage.content = "test"
                  messageType = "test msg"
                  created = DateTimeOffset.UtcNow
                  active = DateTimeOffset.UtcNow
                  expiry = DateTimeOffset.MaxValue }

            try
                let! r = proxy.Post "queue" msg
                failwith "Exception not raised"

            with :? InvalidOperationException as e ->
                ignore 0
        }
