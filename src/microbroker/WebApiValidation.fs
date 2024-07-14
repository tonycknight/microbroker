namespace microbroker

open System
open Giraffe
open Microsoft.AspNetCore.Http

module WebApiValidation =

    let validContentTypes =
        [ System.Net.Mime.MediaTypeNames.Application.Json
          $"{System.Net.Mime.MediaTypeNames.Application.Json}; charset=utf-8" ]

    let isValidQueueName value =
        let p = Char.isAlphaNumeric ||>> Char.isIn [| '-'; '_' |]
        value |> Seq.forall p

    let isValidContentType (ctx: HttpContext) =
        validContentTypes |> Seq.contains ctx.Request.ContentType

    let validateQueueName value =
        match isValidQueueName value with
        | true -> Choice2Of2 value
        | false -> Choice1Of2 { ApiErrorResult.errors = [| $"Invalid queue name '{value}'" |] }

    let getRequest<'a> (ctx: HttpContext) queueId =
        task {
            if isValidQueueName queueId |> not then
                return Choice1Of2 { ApiErrorResult.errors = [| $"Invalid queue name '{queueId}'" |] }
            else if isValidContentType ctx |> not then
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
