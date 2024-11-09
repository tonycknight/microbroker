namespace microbroker

open System

[<CLIMutable>]
type QueueMessage =
    { messageType: string
      content: string
      created: DateTimeOffset
      active: DateTimeOffset
      expiry: DateTimeOffset }
    static member toBsonDoc(data: QueueMessage) =
        let doc = new MongoDB.Bson.BsonDocument()
        doc.["messageType"] <- data.messageType |> MongoDB.Bson.BsonString.Create
        doc.["content"] <- data.content |> MongoDB.Bson.BsonString.Create
        doc.["created"] <- data.created |> MongoDB.Bson.BsonDateTime.Create
        doc.["active"] <- data.active |> MongoDB.Bson.BsonDateTime.Create
        doc.["expiry"] <- data.expiry |> MongoDB.Bson.BsonDateTime.Create
        doc

[<CLIMutable>]
type QueueInfo =
    { name: string
      count: int64
      futureCount: int64 }


[<CLIMutable>]
type ApiErrorResult = { errors: string[] }
