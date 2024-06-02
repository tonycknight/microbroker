namespace microbroker

open System

[<CLIMutable>]
type QueueMessage =
    { messageType: string
      content: string
      created: DateTimeOffset
      active: DateTimeOffset }

[<CLIMutable>]
type QueueInfo =
    { name: string
      count: int64
      futureCount: int64 }


[<CLIMutable>]
type ApiErrorResult = { errors: string[] }
