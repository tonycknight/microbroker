namespace Microbroker.Client.Tests.Unit

open System
open Microbroker.Client
open Xunit
open FsUnit

module MicrobrokerCountTests =
    [<Fact>]
    let ``empty produces named value`` () =
        let name = "aaa"
        let r = MicrobrokerCount.empty name

        r.name |> should equal name

    [<Fact>]
    let ``empty produces zero value`` () =
        let r = MicrobrokerCount.empty "aaa"

        r.count |> should equal 0
        r.futureCount |> should equal 0
