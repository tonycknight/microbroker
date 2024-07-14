namespace microbroker

open System
open Giraffe
open Microsoft.AspNetCore.Http

module WebApiValidation =

    let validContentTypes =
        let json = System.Net.Mime.MediaTypeNames.Application.Json
        [ json; $"{json}; charset=utf-8" ]

    let isValidQueueName value =
        value |> Seq.forall (Char.isAlphaNumeric ||>> Char.isIn [| '-'; '_' |])

    let isValidContentType (ctx: HttpContext) =
        validContentTypes |> Seq.contains ctx.Request.ContentType

    let validateQueueName queueId =
        match isValidQueueName queueId with
        | true -> Choice2Of2 queueId
        | false -> Choice1Of2 { ApiErrorResult.errors = [| $"Invalid queue name '{queueId}'" |] }

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
