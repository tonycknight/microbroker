namespace microbroker.tests.acceptance

open System
open microbroker

module TestUtils =

    [<Literal>]
    let host = "http://localhost:8080"

    [<Literal>]
    let testCollection = "Microbroker acceptance tests"

    [<Literal>]
    let maxClientTests = 5

    [<Literal>]
    let maxServerTests = 5

    let client = new System.Net.Http.HttpClient()

    let jsonContent (json: string) =
        new Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")

    let getQueueInfo (host: string) (queueId: string) =
        task {
            let uri = $"{host}/queues/{queueId}/"
            use! r = client.GetAsync(uri)

            let _ = r.EnsureSuccessStatusCode()

            let! json = r.Content.ReadAsStringAsync()

            return QueueInfoGenerators.fromJson json
        }

    let getQueueInfos (host: string) =
        task {
            let uri = $"{host}/queues/"
            use! r = client.GetAsync(uri)

            let _ = r.EnsureSuccessStatusCode()

            let! json = r.Content.ReadAsStringAsync()

            return QueueInfoGenerators.fromJsonArray json
        }

    let pullAllMessages (host: string) (queueId: string) =
        let rec fetchAll (results: QueueMessage list) =
            task {
                let uri = $"{host}/queues/{queueId}/message/"
                use! getResponse = client.GetAsync(uri)

                if getResponse.StatusCode <> Net.HttpStatusCode.OK then
                    return results
                else
                    let! json = getResponse.Content.ReadAsStringAsync()
                    let message = MessageGenerators.fromJson json

                    return! fetchAll (message :: results)
            }

        fetchAll []

    let pullAllQueueInfos (host: string) (queueId: string) =
        let rec fetchAll results =
            task {

                let! queueInfo = getQueueInfo host queueId
                let results = (queueInfo :: results)

                if queueInfo.count = 0 then
                    return results
                else
                    let uri = $"{host}/queues/{queueId}/message/"
                    use! _ = client.GetAsync(uri)

                    return! fetchAll results
            }

        fetchAll []

[<AutoOpen>]
module TestCombinators =

    let dateTimeOffsetWithLimits (diff: TimeSpan) (x: DateTimeOffset) (y: DateTimeOffset) =
        let delta = Math.Abs((x - y).TotalMilliseconds)

        delta < diff.TotalMilliseconds
