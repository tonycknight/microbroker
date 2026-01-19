module Tests

open System
open Xunit

[<Fact>]
let ``Get Heartbeat`` () =
    task {
        use client = new System.Net.Http.HttpClient()
        
        let uri = "http://localhost:8080/heartbeat/"
        let! r = client.GetAsync(uri)

        let _ = r.EnsureSuccessStatusCode()

        return true

    }

[<Fact>]
let ``Get Queues`` () =
    task {
        use client = new System.Net.Http.HttpClient()
        
        let uri = "http://localhost:8080/queues/"
        let! r = client.GetAsync(uri)

        let _ = r.EnsureSuccessStatusCode()

        return true

    }