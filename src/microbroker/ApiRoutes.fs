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
                        routeCif "/queues/%s/watchers/" (fun id -> WebApi.getQueueWatchers id)
                        routeCi "/queues/" >=> WebApi.getQueues ]
              POST
              >=> choose
                      [ routeCif "/queues/%s/message/" (fun id -> WebApi.postMessage id)
                        routeCif "/queues/%s/messages/" (fun id -> WebApi.postMessages id)
                        routeCif "/queues/%s/watch/%s/" (fun (destination, origin) ->
                            WebApi.linkQueues (destination, origin)) ]
              DELETE
              >=> choose
                      [ routeCif "/queues/%s/" (fun id -> WebApi.deleteQueue id)
                        routeCif "/queues/%s/watch/%s/" (fun (destination, origin) ->
                            WebApi.deleteQueueWatcher (destination, origin)) ] ]
