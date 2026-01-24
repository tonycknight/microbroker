namespace Microbroker.Client.Tests

open System
open Microbroker.Client
open Microbroker.Client.MicrobrokerMessages
open Xunit
open FsUnit

module MicrobrokerMessagesTests =

    [<Fact>]
    let ``create generates vanilla instance`` () =
        let r = create ()

        r.content |> should equal ""
        r.messageType |> should equal ""
        r.active |> should equal DateTimeOffset.MinValue
        r.created |> should greaterThan DateTimeOffset.MinValue

    [<Fact>]
    let ``active applies time`` () =

        let dt = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays 1.)

        let r = create () |> active (fun () -> dt)

        r.active |> should equal dt

    [<Fact>]
    let ``delayed produces time in future`` () =
        let start = DateTimeOffset.UtcNow
        let span = TimeSpan.FromDays 1.
        let finish = start.Add(span).Add(TimeSpan.FromSeconds(1.))

        let r = create () |> delayed (fun () -> span)

        r.active |> should greaterThan start
        r.active |> should lessThan finish

    [<Fact>]
    let ``messageType applies messageType`` () =
        let mt = "abc"
        let r = create () |> messageType mt

        r.messageType |> should equal mt


    [<Fact>]
    let ``content applies content`` () =
        let c = "abc"
        let r = create () |> content c

        r.content |> should equal c

    [<Fact>]
    let ``expiry produces time in future`` () =
        let start = DateTimeOffset.UtcNow
        let span = TimeSpan.FromDays 1.
        let finish = start.Add(span).Add(TimeSpan.FromSeconds(1.))

        let r = create () |> expiry (fun () -> span)

        r.expiry |> should greaterThan start
        r.expiry |> should lessThan finish
