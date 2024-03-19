namespace microbroker

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging

type IQueue =
    abstract member GetNextAsync: unit -> Task<QueueMessage option>
    abstract member PushAsync: message: QueueMessage -> Task
    abstract member GetInfoAsync: unit -> Task<QueueInfo>
    abstract member DeleteAsync: unit -> Task

type IQueueFactory =
    abstract member CreateQueue: name: string -> IQueue

[<CLIMutable>]
type QueueMessageData =
    { _id: MongoDB.Bson.ObjectId
      messageType: string
      content: string
      created: DateTimeOffset }

    static member toQueueMessage(data: QueueMessageData) =
        { QueueMessage.messageType = data.messageType
          content = data.content
          created = data.created }

module MongoQueues =
    [<Literal>]
    let defaultQueueName = "default"

    [<Literal>]
    let queueNamePrefix = "queue__"

type MongoQueue(config: AppConfiguration, logFactory: ILoggerFactory, name) =

    let name = name |> Strings.defaultIf "" MongoQueues.defaultQueueName

    let collectionName = $"{MongoQueues.queueNamePrefix}{name}"
    let logger = logFactory.CreateLogger<MongoQueue>()

    let mongoCol =
        Mongo.initCollection "" config.mongoDbName collectionName config.mongoConnection

    interface IQueue with
        member this.GetInfoAsync() =
            task {
                let! count = Mongo.estimatedCount mongoCol
                return { QueueInfo.name = name; count = count }
            }

        member this.GetNextAsync() =
            task {
                let! data = Mongo.pullSingletonFromQueue<QueueMessageData> mongoCol

                return data |> Option.map QueueMessageData.toQueueMessage
            }

        member this.PushAsync message =
            task { do! [ message ] |> Mongo.pushToQueue mongoCol }

        member this.DeleteAsync() =
            task { Mongo.deleteCollection mongoCol }

type MongoQueueFactory(config: AppConfiguration, logFactory: ILoggerFactory) =
    interface IQueueFactory with
        member this.CreateQueue(name: string) =
            new MongoQueue(config, logFactory, name)

type IQueueProvider =
    abstract member GetQueuesAsync: unit -> Task<QueueInfo[]>
    abstract member GetQueueAsync: queueName: string -> Task<IQueue>
    abstract member DeleteQueueAsync: queueName: string -> Task<bool>

type MongoQueueProvider(config: AppConfiguration, queueFactory: IQueueFactory) =

    let queueColNames () =
        Mongo.findCollectionNames config.mongoDbName config.mongoConnection
        |> Array.filter (fun n -> n.StartsWith(MongoQueues.queueNamePrefix))
        |> Array.map (fun n -> n.Substring(MongoQueues.queueNamePrefix.Length))

    let queues =
        let result =
            new System.Collections.Concurrent.ConcurrentDictionary<string, IQueue>(StringComparer.OrdinalIgnoreCase)

        queueColNames ()
        |> Array.iter (fun n -> result.GetOrAdd(n, queueFactory.CreateQueue) |> ignore)

        result

    let getQueue queueName =
        queues.GetOrAdd(queueName, queueFactory.CreateQueue)

    let deleteQueue queueName =
        match queues.TryGetValue(queueName) with
        | (true, q) ->
            task {
                do! q.DeleteAsync()
                queues.TryRemove(queueName) |> ignore
                return true
            }
        | (false, _) -> task { return false }


    interface IQueueProvider with
        member this.GetQueuesAsync() =
            task {
                let fetches =
                    queues.Values |> Array.ofSeq |> Array.Parallel.map (fun q -> q.GetInfoAsync())

                let! qs = Task.WhenAll fetches

                return qs |> Array.sortBy (fun q -> q.name)
            }

        member this.GetQueueAsync(queueName) = task { return getQueue queueName }

        member this.DeleteQueueAsync(queueName) = deleteQueue queueName
