namespace microbroker.tests.acceptance

open Newtonsoft.Json
open FsCheck.Xunit

// TODO: [<Xunit.Collection(TestUtils.testCollection)>]
module MetadataApiTests =

    [<Property(MaxTest = 1)>]
    let ``GET Heartbeat returns OK`` () =
        task {
            let uri = $"{TestUtils.host}/heartbeat/"
            use! response = TestUtils.client.GetAsync(uri)

            let _ = response.EnsureSuccessStatusCode()

            let! json = response.Content.ReadAsStringAsync()

            let result = JsonConvert.DeserializeObject<string[]>(json)

            return result |> Array.head = "OK"
        }
