namespace microbroker.tests.acceptance

open System
open microbroker

module TestUtils =

    let client = new System.Net.Http.HttpClient()

    let jsonContent (json: string) =
        new Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")

    let pullAll (host: string) (queueId: string) =
        let rec fetchAll (results: QueueMessage list) =
            task {
                let uri = $"{host}/queues/{queueId}/message/"
                use! getResponse = client.GetAsync(uri)

                if getResponse.StatusCode = Net.HttpStatusCode.NotFound then
                    return results
                else
                    let! json = getResponse.Content.ReadAsStringAsync()
                    let message = MessageGenerators.fromJson json

                    return! fetchAll (message :: results)
            }

        fetchAll []

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


[<AutoOpen>]
module TestCombinators =

    let dateTimeOffsetWithLimits (x: DateTimeOffset) (y: DateTimeOffset) =
        let delta = Math.Abs((x - y).TotalMilliseconds)

        delta < (1000.)
