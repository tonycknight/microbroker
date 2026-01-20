namespace microbroker

open System
open Giraffe
open Microsoft.AspNetCore.Http

module WebApiValidation =

    let validContentTypes =
        let json = System.Net.Mime.MediaTypeNames.Application.Json
        [ json; $"{json}; charset=utf-8" ]

    let isValidQueueName (value: string) =
        value.Length > 0
        && value |> Seq.forall (Char.isAlphaNumeric ||>> Char.isIn [| '-'; '_' |])

    let isValidContentType (ctx: HttpContext) =
        validContentTypes |> Seq.contains ctx.Request.ContentType

    let validateQueueName queueId =
        match isValidQueueName queueId with
        | true -> Choice2Of2 queueId
        | false -> Choice1Of2 { ApiErrorResult.errors = [| $"Invalid queue name '{queueId}'" |] }

    let getRequest<'a> (ctx: HttpContext) queueId =
        task {
            match validateQueueName queueId with
            | Choice1Of2 error -> return Choice1Of2 error
            | _ ->
                if isValidContentType ctx |> not then
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
