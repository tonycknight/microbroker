namespace microbroker

open System
open System.Threading.Tasks

type IQueue =
    abstract member GetNextAsync: unit -> Task<QueueMessage option>
    abstract member PushAsync: message: QueueMessage -> Task
    abstract member GetInfoAsync: unit -> Task<QueueInfo>

type IQueueFactory =
    abstract member CreateQueue: name: string -> IQueue

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

type MongoQueue(config: AppConfiguration, name) =
    interface IQueue with
        member this.GetInfoAsync() =
            task {
                return { QueueInfo.name = name; count = 0 }
            }

        member this.GetNextAsync() = task { return None }

        member this.PushAsync message = task { ignore 0 }

type QueueFactory(config: AppConfiguration) =
    interface IQueueFactory with
        member this.CreateQueue(name: string) = new MemoryQueue(name)

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
