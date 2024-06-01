namespace microbroker

open System
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection

module WebApi =
    let queueProvider (ctx: HttpContext) =
        ctx.RequestServices.GetRequiredService<IQueueProvider>()

    let queue (name: string) (queueProvider: IQueueProvider) = queueProvider.GetQueueAsync name

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
                let! q = queueProvider ctx |> queue name

                let! info = q.GetInfoAsync()

                return! Successful.ok (info |> json) next ctx
            }

    let getMessage (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
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
                match! WebApiValidation.getRequest<QueueMessage> ctx with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 msg ->
                    let! q = queueProvider ctx |> queue queueId

                    let msg =
                        { msg with
                            created = DateTimeOffset.UtcNow }

                    do! q.PushAsync msg
                    return! Successful.NO_CONTENT next ctx
            }

    let postMessages (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let rec loop (q: IQueue) msgs =
                task {
                    match msgs with
                    | [] -> return ignore 0
                    | h :: ms ->
                        do! q.PushAsync h
                        return! loop q ms
                }

            task {
                match! WebApiValidation.getRequest<QueueMessage[]> ctx with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 msgs ->
                    let! q = queueProvider ctx |> queue queueId

                    let msgs =
                        msgs
                        |> Seq.map (fun m ->
                            { m with
                                created = DateTimeOffset.UtcNow })
                        |> List.ofSeq

                    do! loop q msgs

                    return! Successful.NO_CONTENT next ctx
            }

    let deleteQueue (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let qp = queueProvider ctx

                let! r = qp.DeleteQueueAsync queueId

                return!
                    if r then
                        Successful.NO_CONTENT next ctx
                    else
                        ctx.SetStatusCode StatusCodes.Status404NotFound
                        next ctx
            }
