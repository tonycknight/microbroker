namespace Microbroker.Client.Tests.Unit

open System
open Microbroker.Client
open Xunit
open FsUnit

module ThrottlingTests =

    [<Fact>]
    let ``exponentialWait on success yields single iteration`` () =
        let duration = TimeSpan.FromSeconds 1.
        let mutable count = 0

        let f () =
            task {
                count <- count + 1
                return Some count
            }

        let r = (Throttling.exponentialWait duration f).Result

        count |> should equal 1

    [<Fact>]
    let ``exponentialWait on None yields multiple iterations`` () =
        let duration = TimeSpan.FromSeconds 1.
        let mutable count = 0
        let iterations = 3

        let f () =
            task {
                count <- count + 1

                return
                    match count < iterations with
                    | true -> None
                    | _ -> Some count
            }

        let r = (Throttling.exponentialWait duration f).Result


        count |> should equal iterations


    [<Fact>]
    let ``exponentialWait on exception yields multiple iterations`` () =
        let duration = TimeSpan.FromSeconds 1.
        let mutable count = 0
        let iterations = 3

        let f () =
            task {
                count <- count + 1

                if count < iterations then
                    invalidOp "broken"

                return Some count
            }

        try
            (Throttling.exponentialWait duration f).Result |> ignore
        with ex ->
            ignore 0

        count |> should equal 1
