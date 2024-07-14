namespace microbroker

open System
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection

module WebApi =
    let private queueProvider (ctx: HttpContext) =
        ctx.RequestServices.GetRequiredService<IQueueProvider>()

    let private queue (name: string) (queueProvider: IQueueProvider) = queueProvider.GetQueueAsync name

    let private pushToQueues (queues: seq<IQueue>) (msg: QueueMessage) =
        task {
            let pushes = queues |> Seq.map (fun q -> q.PushAsync msg) |> Array.ofSeq

            do! System.Threading.Tasks.Task.WhenAll pushes
        }

    let rec private pushManyToQueues queues msgs =
        task {
            match msgs with
            | [] -> return ignore 0
            | h :: ms ->
                do! pushToQueues queues h
                return! pushManyToQueues queues ms
        }

    let getQueues =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let qp = queueProvider ctx
                let! qs = qp.GetQueuesAsync()

                return! Successful.ok (qs |> Array.sortBy _.name |> json) next ctx
            }

    let getQueueInfo name =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match WebApiValidation.validateQueueName name with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 name ->
                    let! q = queueProvider ctx |> queue name

                    let! info = q.GetInfoAsync()

                    return! Successful.ok (info |> json) next ctx
            }

    let getMessage (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match WebApiValidation.validateQueueName queueId with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 queueId ->
                    let! q = queueProvider ctx |> queue queueId

                    match! q.GetNextAsync() with
                    | None ->
                        ctx.SetStatusCode StatusCodes.Status404NotFound
                        return! next ctx
                    | Some msg -> return! Successful.OK msg next ctx
            }

    let postMessage (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match! WebApiValidation.getRequest<QueueMessage> ctx queueId with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 msg ->
                    let! q = queueProvider ctx |> queue queueId

                    let msg =
                        { msg with
                            created = DateTimeOffset.UtcNow }

                    do! pushManyToQueues [ q ] [ msg ]

                    return! Successful.NO_CONTENT next ctx
            }

    let postMessages (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match! WebApiValidation.getRequest<QueueMessage[]> ctx queueId with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 msgs ->
                    let qp = queueProvider ctx
                    let! q = qp |> queue queueId

                    let msgs =
                        msgs
                        |> Seq.map (fun m ->
                            { m with
                                created = DateTimeOffset.UtcNow })
                        |> List.ofSeq

                    do! pushManyToQueues [ q ] msgs

                    return! Successful.NO_CONTENT next ctx
            }

    let deleteQueue (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match WebApiValidation.validateQueueName queueId with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 queueId ->
                    let qp = queueProvider ctx

                    let! r = qp.DeleteQueueAsync queueId

                    return!
                        if r then
                            Successful.NO_CONTENT next ctx
                        else
                            ctx.SetStatusCode StatusCodes.Status404NotFound
                            next ctx
            }

    let linkQueues (queueId: string, originQueueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match
                    (WebApiValidation.validateQueueName queueId, WebApiValidation.validateQueueName originQueueId)
                with
                | Choice1Of2 error, _ -> return! RequestErrors.BAD_REQUEST error next ctx
                | _, Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 queueId, _ ->
                    try
                        let qp = queueProvider ctx
                        let! r = qp.LinkQueuesAsync originQueueId queueId

                        return! Successful.NO_CONTENT next ctx
                    with :? System.ArgumentException as ex ->
                        return! RequestErrors.BAD_REQUEST [ ex.Message ] next ctx
            }

    let getQueueWatchers (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match WebApiValidation.validateQueueName queueId with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 queueId ->
                    let qp = queueProvider ctx

                    let! qs = qp.GetLinkedQueues queueId

                    let r = qs |> Seq.map _.Name |> Seq.sortBy id |> Array.ofSeq

                    return! Successful.OK r next ctx
            }

    let deleteQueueWatcher (destinationQueueId: string, originQueueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                match
                    (WebApiValidation.validateQueueName destinationQueueId,
                     WebApiValidation.validateQueueName originQueueId)
                with
                | Choice1Of2 error, _ -> return! RequestErrors.BAD_REQUEST error next ctx
                | _, Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 queueId, _ ->
                    let qp = queueProvider ctx

                    let! r = qp.DeleteLinkedQueuesAsync originQueueId destinationQueueId

                    return!
                        if r then
                            Successful.NO_CONTENT next ctx
                        else
                            ctx.SetStatusCode StatusCodes.Status404NotFound
                            next ctx
            }
