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
