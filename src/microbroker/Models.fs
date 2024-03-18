namespace microbroker

open System

[<CLIMutable>]
type QueueMessage =
    { id: Guid
      priority: decimal
      messageType: string
      content: string
      created: DateTimeOffset
      ttl: DateTimeOffset option }

[<CLIMutable>]
type QueueInfo = { name: string; count: int64 }


[<CLIMutable>]
type ApiErrorResult = { errors: string[] }
