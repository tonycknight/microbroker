namespace microbroker

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging

type IQueue =
    abstract member GetNextAsync: unit -> Task<QueueMessage option>
    abstract member PushAsync: message: QueueMessage -> Task
    abstract member GetInfoAsync: unit -> Task<QueueInfo>

type IQueueFactory =
    abstract member CreateQueue: name: string -> IQueue

[<CLIMutable>]
type QueueMessageData =
    { _id: MongoDB.Bson.ObjectId
      priority: decimal
      messageType: string
      content: string
      created: DateTimeOffset }

    static member toQueueMessage(data: QueueMessageData) =
        { QueueMessage.priority = data.priority
          messageType = data.messageType
          content = data.content
          created = data.created }

type MemoryQueue(name: string) =

    let queue = new System.Collections.Concurrent.ConcurrentQueue<QueueMessage>()

    interface IQueue with
        member this.GetInfoAsync() =
            task {
                return
                    { QueueInfo.name = name
                      count = queue.Count }
            }

        member this.GetNextAsync() =
            task {
                return
                    match queue.TryDequeue() with
                    | true, msg -> Some msg
                    | false, _ -> None
            }

        member this.PushAsync message = task { queue.Enqueue message }

type MongoQueue(config: AppConfiguration, logFactory: ILoggerFactory, name) =
    [<Literal>]
    let defaultQueueName = "default"

    [<Literal>]
    let queueNamePrefix = $"queue__"

    let name = name |> Strings.defaultIf "" defaultQueueName

    let collectionName = $"{queueNamePrefix}{name}"
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

type QueueFactory(config: AppConfiguration, logFactory: ILoggerFactory) =
    interface IQueueFactory with
        member this.CreateQueue(name: string) =
            new MongoQueue(config, logFactory, name)

type IQueueProvider =
    // TODO: return names only? because the counts, with large numbers of queues, will cause spammed DB requests
    abstract member GetQueuesAsync: unit -> Task<QueueInfo[]>
    abstract member GetQueueAsync: queueName: string -> Task<IQueue>

type QueueProvider(queueFactory: IQueueFactory) =

    let queues =
        new System.Collections.Concurrent.ConcurrentDictionary<string, IQueue>(StringComparer.OrdinalIgnoreCase)

    let getQueue queueName =
        queues.GetOrAdd(queueName, queueFactory.CreateQueue)

    interface IQueueProvider with
        member this.GetQueuesAsync() =
            task {
                let qis =
                    queues.Values |> Array.ofSeq |> Array.Parallel.map (fun q -> q.GetInfoAsync())

                return! Task.WhenAll qis
            }

        member this.GetQueueAsync(queueName) = task { return getQueue queueName }
