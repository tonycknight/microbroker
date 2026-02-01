namespace Microbroker.Client

open System.Net
open System.Threading.Tasks

type MicrobrokerCount =
    { name: string
      count: int64
      futureCount: int64 }

    static member empty(name: string) =
        { MicrobrokerCount.name = name
          count = 0
          futureCount = 0 }

type IMicrobrokerProxy =
    abstract member PostAsync: string -> MicrobrokerMessage -> Task<unit>
    abstract member PostManyAsync: string -> seq<MicrobrokerMessage> -> Task<unit>
    abstract member GetNextAsync: string -> Task<MicrobrokerMessage option>
    abstract member GetQueueCountsAsync: string[] -> Task<MicrobrokerCount[]>
    abstract member GetQueueCountAsync: string -> Task<MicrobrokerCount option>

type internal MicrobrokerProxy(config: MicrobrokerConfiguration, httpClient: IHttpClient) =
    let onError resp =
        match resp with
        | HttpErrorRequestResponse(status, _, _, _) when status = HttpStatusCode.NotFound -> None
        | HttpErrorRequestResponse(status, body, _, errors) ->
            let msg =
                match errors.errors |> Strings.join System.Environment.NewLine with
                | "" -> $"{status} received from server: {body}"
                | xs -> xs

            invalidOp msg

        | HttpExceptionRequestResponse ex -> raise ex
        | HttpBadGatewayResponse _ -> invalidOp "Server is unavailable"
        | HttpTooManyRequestsResponse _ -> invalidOp "Server is unavailable - too many requests"
        | _ -> invalidOp $"Unrecognised response {resp}"

    let getNext (queue: string) =
        task {
            let url = $"{config.brokerBaseUrl |> Strings.trimSlash}/queues/{queue}/message/"
            let! resp = httpClient.GetAsync url

            return
                match resp with
                | HttpOkRequestResponse(status, body, _, _) when status = HttpStatusCode.OK ->
                    MicrobrokerMessages.fromString body
                | HttpOkRequestResponse(status, _, _, _) -> None
                | _ -> onError resp
        }


    let postMany (queue: string) (messages: seq<MicrobrokerMessage>) =
        task {
            let messages = Array.ofSeq messages

            if messages.Length > 0 then
                let brokerMessages = MicrobrokerMessages.toJsonArray messages

                let url = $"{Strings.trimSlash config.brokerBaseUrl}/queues/{queue}/messages/"

                match! httpClient.PostAsync url brokerMessages with
                | HttpOkRequestResponse _ -> ignore 0
                | resp -> onError resp |> ignore
        }

    let queueCount queue =
        task {
            let url = $"{Strings.trimSlash config.brokerBaseUrl}/queues/{queue}/"
            let! resp = httpClient.GetAsync url

            return
                match resp with
                | HttpOkRequestResponse(_, body, _, _) ->
                    Newtonsoft.Json.JsonConvert.DeserializeObject<MicrobrokerCount>(body) |> Some
                | resp -> onError resp
        }

    interface IMicrobrokerProxy with
        member this.PostAsync queue message = postMany queue [ message ]

        member this.PostManyAsync queue messages = postMany queue messages

        member this.GetNextAsync queue =
            Throttling.exponentialWait config.throttleMaxTime (fun () -> getNext queue)

        member this.GetQueueCountsAsync(queues: string[]) =
            task {
                let tasks = queues |> Array.map queueCount

                let! counts = Task.WhenAll tasks

                return counts |> Array.filter (fun c -> c.IsSome) |> Array.map (fun c -> c.Value)
            }

        member this.GetQueueCountAsync queue = queueCount queue
