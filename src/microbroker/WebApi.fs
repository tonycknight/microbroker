namespace microbroker

open System
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection

module WebApi=
    let qp (ctx: HttpContext) = ctx.RequestServices.GetRequiredService<IQueueProvider>()
    let q (name: string) (queueProvider: IQueueProvider) = queueProvider.GetQueueAsync name

    let getQueues = 
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {               
                let qs = qp ctx
                let! qs = qs.GetQueuesAsync ()
                
                return! Successful.ok (qs |> Array.sortBy _.name |> json) next ctx
            }

    let getMessage (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {               
                let! q = qp ctx |> q queueId
                                
                match! q.GetNextAsync() with
                | None -> return! RequestErrors.NOT_FOUND "" next ctx
                | Some msg -> return! Successful.OK msg next ctx
            }

    let postMessage (queueId: string) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {                
                match! WebApiValidation.getRequest<QueueMessage> ctx with
                | Choice1Of2 error -> return! RequestErrors.BAD_REQUEST error next ctx
                | Choice2Of2 msg -> 
                    let! q = qp ctx |> q queueId
                    let msg =   if msg.id = Guid.Empty then
                                    { msg with id = Guid.NewGuid()}
                                else
                                    msg

                    do! q.PushAsync msg                    
                    return! Successful.OK "" next ctx
            }
