namespace microbroker

open System
open Giraffe

module ApiRoutes =
    let private favicon =
        GET
        >=> route "/favicon.ico"
        >=> ResponseCaching.publicResponseCaching 999999 None
        >=> Successful.NO_CONTENT

    let private heartbeat =
        GET
        >=> route "/heartbeat/"
        >=> ResponseCaching.noResponseCaching
        >=> json [ "OK" ]

    let webApp (sp: IServiceProvider) =
        choose
            [ favicon
              GET
              >=> ResponseCaching.noResponseCaching
              >=> choose
                      [ heartbeat
                        routeCif "/queues/%s/" (fun id -> WebApi.getQueueInfo id)
                        routeCif "/queues/%s/message/" (fun id -> WebApi.getMessage id)
                        routeCi "/queues/" >=> WebApi.getQueues ]
              POST
              >=> choose
                      [ routeCif "/queues/%s/message/" (fun id -> WebApi.postMessage id)
                        routeCif "/queues/%s/messages/" (fun id -> WebApi.postMessages id) ]
              DELETE >=> choose [ routeCif "/queues/%s/" (fun id -> WebApi.deleteQueue id) ] ]
