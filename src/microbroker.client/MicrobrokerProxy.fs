namespace Microbroker.Client

open System.Net
open System.Threading.Tasks
open Microsoft.Extensions.Logging

type MicrobrokerCount =
    { name: string
      count: int64
      futureCount: int64 }

    static member empty(name: string) =
        { MicrobrokerCount.name = name
          count = 0
          futureCount = 0 }

type IMicrobrokerProxy =
    abstract member Post: string -> MicrobrokerMessage -> Task<unit>
    abstract member PostMany: string -> seq<MicrobrokerMessage> -> Task<unit>
    abstract member GetNext: string -> Task<MicrobrokerMessage option>
    abstract member GetQueueCounts: string[] -> Task<MicrobrokerCount[]>
    abstract member GetQueueCount: string -> Task<MicrobrokerCount option>

type internal MicrobrokerProxy(config: MicrobrokerConfiguration, httpClient: IHttpClient, logger: ILoggerFactory) =
    let log = logger.CreateLogger<MicrobrokerProxy>()

    let getNext (queue: string) =
        task {
            let url = $"{config.brokerBaseUrl |> Strings.trimSlash}/queues/{queue}/message/"
            let! resp = httpClient.GetAsync url

            return
                match resp with
                | HttpOkRequestResponse(status, body, _, _) when status = HttpStatusCode.OK ->
                    MicrobrokerMessages.fromString body
                | HttpOkRequestResponse(status, _, _, _) -> None
                | HttpErrorRequestResponse(status, body, _, _) when status = HttpStatusCode.NotFound -> None
                | HttpErrorRequestResponse(status, body, _, errors) ->
                    let msg =
                        match errors.errors |> Strings.join System.Environment.NewLine with
                        | "" -> $"{status} received from server: {body}"
                        | xs -> xs

                    raise (System.InvalidOperationException(msg))

                | HttpExceptionRequestResponse ex -> raise ex
                | HttpBadGatewayResponse _ -> None
                | HttpTooManyRequestsResponse _ -> None
        }


    let postMany (queue: string) (messages: seq<MicrobrokerMessage>) =
        task {
            let messages = Array.ofSeq messages

            if messages.Length > 0 then
                let brokerMessages = MicrobrokerMessages.toJsonArray messages

                let url = $"{Strings.trimSlash config.brokerBaseUrl}/queues/{queue}/messages/"

                try
                    match! httpClient.PostAsync url brokerMessages with
                    | HttpOkRequestResponse _ -> $"{messages.Length} messages sent to broker" |> log.LogDebug
                    | resp -> HttpRequestResponse.loggable resp |> log.LogError

                with ex ->
                    log.LogError(ex, ex.Message)
        }

    let queueCount queue =
        task {
            let url = $"{Strings.trimSlash config.brokerBaseUrl}/queues/{queue}/"
            let! resp = httpClient.GetAsync url

            return
                match resp with
                | HttpOkRequestResponse(_, body, _, _) ->
                    Newtonsoft.Json.JsonConvert.DeserializeObject<MicrobrokerCount>(body) |> Some
                | resp ->
                    HttpRequestResponse.loggable resp |> log.LogError
                    None
        }

    interface IMicrobrokerProxy with
        member this.Post queue message = postMany queue [ message ]

        member this.PostMany queue messages = postMany queue messages

        member this.GetNext queue =
            Throttling.exponentialWait config.throttleMaxTime (fun () -> getNext queue)

        member this.GetQueueCounts(queues: string[]) =
            task {
                let tasks = queues |> Array.map queueCount

                let! counts = Task.WhenAll tasks

                return counts |> Array.filter (fun c -> c.IsSome) |> Array.map (fun c -> c.Value)
            }

        member this.GetQueueCount queue = queueCount queue
