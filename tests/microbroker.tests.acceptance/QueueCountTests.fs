namespace microbroker.tests.acceptance

open System
open FsCheck.Xunit
open microbroker

module QueueCountTests =

    [<Property(MaxTest = 1)>]
    let ``GET Queues returns array`` () =
        task {
            let! result = TestUtils.getQueueInfos TestUtils.host

            return
                result |> Array.length >= 0
                && result |> Array.forall (fun r -> r.name |> String.IsNullOrWhiteSpace |> not)
                && result |> Array.forall (fun r -> r.count >= 0)
                && result |> Array.forall (fun r -> r.futureCount >= 0)
        }
