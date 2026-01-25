namespace Microbroker.Client

open System
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

    let getNextFromBroker (queue: string) =
        task {
            let url = $"{config.brokerBaseUrl |> Strings.trimSlash}/queues/{queue}/message/"
            let! rep = httpClient.GetAsync url

            let result =
                match rep with
                | HttpOkRequestResponse(_, body, _, _) -> MicrobrokerMessages.fromString body

                | HttpErrorRequestResponse(status, _, _) when status = HttpStatusCode.NotFound -> None
                | _ ->
                    HttpRequestResponse.loggable rep |> log.LogError
                    None

            return result
        }


    let postManyToBroker (queue: string) (messages: seq<MicrobrokerMessage>) =
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

    let queueCounts () =
        task {
            let url = $"{Strings.trimSlash config.brokerBaseUrl}/queues/"
            let! resp = httpClient.GetAsync url

            return
                match resp with
                | HttpOkRequestResponse(_, body, _, _) ->
                    Newtonsoft.Json.JsonConvert.DeserializeObject<MicrobrokerCount[]>(body)
                | resp ->
                    HttpRequestResponse.loggable resp |> log.LogError
                    Array.empty
        }

    let filteredQueueCounts queues =
        task {
            let! counts = queueCounts ()

            let isMatch (qc: MicrobrokerCount) =
                queues
                |> Seq.exists (fun q -> StringComparer.InvariantCultureIgnoreCase.Equals(qc.name, q))

            return counts |> Array.filter isMatch
        }

    interface IMicrobrokerProxy with
        member this.Post queue message = postManyToBroker queue [ message ]

        member this.PostMany queue messages = postManyToBroker queue messages

        member this.GetNext queue =
            Throttling.exponentialWait config.throttleMaxTime (fun () -> getNextFromBroker queue)

        member this.GetQueueCounts(queues: string[]) = filteredQueueCounts queues

        member this.GetQueueCount queue =
            task {
                let! counts = filteredQueueCounts [| queue |]

                return
                    match counts with
                    | [| c |] -> Some c
                    | _ -> None
            }
