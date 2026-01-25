namespace Microbroker.Client

open System
open System.Threading.Tasks

module internal Throttling =

    let exponentialWait duration func =
        let finish = DateTime.UtcNow + duration
        let mutable wait = TimeSpan.FromMilliseconds 100.

        let rec loop iteration =
            task {
                match! func () with
                | Some r -> return Some r
                | _ ->
                    let now = DateTime.UtcNow

                    if now >= finish then
                        return None
                    else
                        wait <- wait + wait
                        do! Task.Delay wait
                        return! loop (iteration + 1.)
            }

        loop 0.
