namespace microbroker

open System
open Giraffe
open Microsoft.AspNetCore.Http

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
                        routeCif "/queues/%s/" (fun id -> WebApi.getMessage id)
                        routeCi "/queues/" >=> WebApi.getQueues ]
              POST >=> choose [ routeCif "/queues/%s/" (fun id -> WebApi.postMessage id) ] ]
