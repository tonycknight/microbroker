namespace microbroker

open System

[<CLIMutable>]
type QueueMessage =
    { priority: decimal
      messageType: string
      content: string
      created: DateTimeOffset }

[<CLIMutable>]
type QueueInfo = { name: string; count: int64 }


[<CLIMutable>]
type ApiErrorResult = { errors: string[] }
