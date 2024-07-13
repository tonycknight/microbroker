namespace microbroker

open System
open Giraffe
open Microsoft.AspNetCore.Http

module WebApiValidation =
    
    let getRequest<'a> (ctx: HttpContext) =
        task {
            if
                ctx.Request.ContentType <> System.Net.Mime.MediaTypeNames.Application.Json
                && ctx.Request.ContentType
                   <> $"{System.Net.Mime.MediaTypeNames.Application.Json}; charset=utf-8"
            then
                return Choice1Of2 { ApiErrorResult.errors = [| "Invalid content type" |] }
            else
                try
                    let! msg = ctx.BindModelAsync<'a>()

                    return
                        match System.Object.ReferenceEquals(msg, null) with
                        | false -> Choice2Of2 msg
                        | true -> Choice1Of2 { ApiErrorResult.errors = [| "Invalid request" |] }
                with ex ->
                    return Choice1Of2 { ApiErrorResult.errors = [| "Invalid request" |] }
        }

    let validateQueueName value =
        let isMatch value = 
            let p = Char.isAlphaNumeric ||>> Char.isIn [| '-'; '_' |]
            value |> Seq.forall p
                
        match isMatch value with
        | true -> Choice2Of2 value
        | false -> Choice1Of2 { ApiErrorResult.errors = [|  $"Invalid queue name {value}" |] }