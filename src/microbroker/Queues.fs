namespace microbroker

open System
open System.Threading.Tasks
open microbroker.Strings
open Microsoft.Extensions.Logging

type IQueue =
    abstract member GetNextAsync: unit -> Task<QueueMessage option>
    abstract member PushAsync: message: QueueMessage -> Task
    abstract member GetInfoAsync: unit -> Task<QueueInfo>
    abstract member DeleteAsync: unit -> Task
    abstract member Name: string

type IQueueProvider =
    abstract member GetQueuesAsync: unit -> Task<QueueInfo[]>
    abstract member GetQueueAsync: queueName: string -> Task<IQueue>
    abstract member DeleteQueueAsync: queueName: string -> Task<bool>
    abstract member LinkQueuesAsync: originQueueName: string -> destinationQueueName: string -> Task<bool>
    abstract member GetLinkedQueues: queueName: string -> Task<IQueue list>
    abstract member DeleteLinkedQueuesAsync: originQueueName: string -> destinationQueueName: string -> Task<bool>

type ILinkedQueueProvider =
    abstract member LinkQueuesAsync: originQueueName: string -> destinationQueueName: string -> Task<bool>
    abstract member GetLinkedQueuesAsync: queueName: string -> Task<string list>
    abstract member GetLinkedToQueuesAsync: queueName: string -> Task<string list>
    abstract member DeletedLinkedQueuesAsync: originQueueName: string -> destinationQueueName: string -> Task<bool>

type IQueueMessageRelay =
    abstract member RelayAsync: originQueueName: string -> msgs: QueueMessage[] -> Task<bool>

[<CLIMutable>]
type QueueMessageData =
    { _id: MongoDB.Bson.ObjectId
      messageType: string
      content: string
      created: DateTimeOffset
      active: DateTimeOffset
      expiry: DateTimeOffset }

    static member toQueueMessage(data: QueueMessageData) =
        { QueueMessage.messageType = data.messageType
          content = data.content
          created = data.created
          active = data.active
          expiry = data.expiry }

[<CLIMutable>]
type LinkedQueue =
    { _id: obj
      originQueueName: string
      destinationQueueName: string }

module MongoQueues =
    [<Literal>]
    let defaultQueueName = "default"

    [<Literal>]
    let queueNamePrefix = "queue__"

    [<Literal>]
    let ttaQueueNamePrefix = "ttaqueue__"

    [<Literal>]
    let linkedQueuesName = "linkedqueues"

type MongoQueue(config: AppConfiguration, logFactory: ILoggerFactory, relay: IQueueMessageRelay, name) =

    let name = name |> Strings.defaultIf "" MongoQueues.defaultQueueName

    let activeQueueCollectionName = $"{MongoQueues.queueNamePrefix}{name}"
    let ttaQueueCollectionName = $"{MongoQueues.ttaQueueNamePrefix}{name}"
    let log = logFactory.CreateLogger<MongoQueue>()
        
    let setExpiry (msg: QueueMessage) =
        if msg.expiry = DateTimeOffset.MinValue then
            { msg with
                expiry = DateTimeOffset.MaxValue }
        else
            msg

    let isExpired (msg: QueueMessageData) =
        msg.expiry > DateTimeOffset.MinValue && msg.expiry < DateTimeOffset.UtcNow

    let activeQueueMongoCol =
        Mongo.initCollection "" config.mongoDbName activeQueueCollectionName config.mongoConnection

    let ttaQueueMongoCol =
        Mongo.initCollection "active" config.mongoDbName ttaQueueCollectionName config.mongoConnection

    let relayToLinkedQueues msgs =
        task {
            let! r = relay.RelayAsync name msgs
            
            ignore r
        }

    let moveTtaMessagesToActive () =
        task {
            let cutOff = DateTimeOffset.UtcNow
            let! msgs = cutOff |> Mongo.pullFromTta<QueueMessageData> ttaQueueMongoCol
            let mutable totalMoved = 0L

            for batch in msgs |> Seq.chunkBySize 100 do

                do!
                    batch
                    |> Seq.map (fun m ->
                        { m with
                            _id = new MongoDB.Bson.ObjectId(Guid.NewGuid().ToString().Replace("-", "")) })
                    |> Mongo.pushToQueue activeQueueMongoCol 

                let ids = batch |> Seq.map (fun m -> $"ObjectId('{m._id}')") |> Strings.join ", "
                let predicate = ids |> sprintf "{ '_id':  { $in: [%s] } }"

                let! deletions = predicate |> Mongo.deleteFromQueue ttaQueueMongoCol

                do! batch |> Array.map QueueMessageData.toQueueMessage |> relayToLinkedQueues

                totalMoved <- totalMoved + deletions

            return totalMoved
        }

    let createMoveTimer () =
        let interval = TimeSpan.FromMinutes(1.)
        let moveTimer = new System.Timers.Timer(interval)

        let moveCallback (x) =
            try
                $"Starting TTA move for queue [{name}]..." |> log.LogTrace
                let deletions = moveTtaMessagesToActive().Result
                $"{deletions} messages moved for queue [{name}]." |> log.LogInformation
            with ex ->
                log.LogError(ex, ex.Message)

        moveTimer.Elapsed.Add moveCallback
        moveTimer

    let moveTimer = createMoveTimer ()
    do moveTimer.Enabled <- true
    do moveTimer.Start()

    interface IQueue with
        member this.Name = name

        member this.GetInfoAsync() =
            task {
                let! activeCount = Mongo.estimatedCount activeQueueMongoCol
                let! ttaCount = Mongo.estimatedCount ttaQueueMongoCol

                return
                    { QueueInfo.name = name
                      count = activeCount
                      futureCount = ttaCount }
            }

        member this.GetNextAsync() =
            let rec getNext () =
                task {
                    let! data = Mongo.pullSingletonFromQueue<QueueMessageData> activeQueueMongoCol

                    return!
                        match data with
                        | Some d when (isExpired d |> not) -> task { return QueueMessageData.toQueueMessage d |> Some }
                        | None -> task { return None }
                        | Some d -> getNext ()
                }

            getNext ()

        member this.PushAsync message =
            task {
                let (relay,col) =
                    if message.active > DateTimeOffset.UtcNow then
                        (false,ttaQueueMongoCol)
                    else
                        (true,activeQueueMongoCol)
                try
                    let messages = [| setExpiry message |]
                    do! messages |> Mongo.pushToQueue col
                    if relay then
                        do! relayToLinkedQueues messages
                with ex ->
                    $"Queue [{name}] - error {ex.Message}" |> log.LogError
            }

        member this.DeleteAsync() =
            task {
                moveTimer.Enabled <- false
                moveTimer.Stop()
                Mongo.deleteCollection activeQueueMongoCol
                Mongo.deleteCollection ttaQueueMongoCol
                moveTimer.Dispose()
            }

type QueueMessageRelay(queueProvider: IQueueProvider) =
    interface IQueueMessageRelay with
        member this.RelayAsync origin messages = 
            task {
                let! destinations = queueProvider.GetLinkedQueues origin

                let sends = 
                    destinations 
                    |> Seq.collect (fun q -> messages |> Seq.map (fun m -> q.PushAsync m ) ) 
                    |> Array.ofSeq

                // TODO: blocking call - should be entirely asynch. Some queues perhaps?
                do! System.Threading.Tasks.Task.WhenAll sends

                return true
            }

type MongoLinkedQueueProvider(config: AppConfiguration, logFactory: ILoggerFactory)=
    
    let col =
        Mongo.initCollection "originQueueName" config.mongoDbName MongoQueues.linkedQueuesName config.mongoConnection
        |> Mongo.setIndex "destinationQueueName"

    let id origin destination = $"{origin}:::{destination}"

    let add origin destination = 
        task {
            
            let x = { LinkedQueue._id = (id origin destination); originQueueName = origin; destinationQueueName = destination}

            let! r = x |> MongoBson.ofObject |> Mongo.upsert col
            
            return r.IsAcknowledged
        }

    let delete origin destination = 
        task {            
            let! r = (id origin destination) |> Mongo.deleteSingle col
            
            return r.IsAcknowledged && r.DeletedCount > 0
        }

    let getFrom origin = 
        task {
            let! xs = $"{{ originQueueName: '{origin}' }}" |> Mongo.getMany<LinkedQueue> col
            
            return xs |> List.ofSeq
        }

    let getTo origin =
        task {
            let! xs = $"{{ destinationQueueName: '{origin}' }}" |> Mongo.getMany<LinkedQueue> col
            
            return xs |> List.ofSeq
        }
    
    interface ILinkedQueueProvider with        
        member this.LinkQueuesAsync originQueueName destinationQueueName = 
            task {
                if originQueueName ==~ destinationQueueName then    
                    invalidArg "" "Cannot reference self."
                                
                let! existing = getFrom destinationQueueName 
                if existing |> Seq.exists (fun x -> x.destinationQueueName ==~ originQueueName) then
                    invalidArg "" "Cannot create a circular reference."

                return! add originQueueName destinationQueueName                
            }

        member this.GetLinkedQueuesAsync queueName = 
            task {
                let! xs = getFrom queueName 
                return xs |> List.map _.destinationQueueName
            }

        member this.GetLinkedToQueuesAsync queueName =
            task {
                let! xs = getTo queueName 
                return xs |> List.map _.originQueueName
            }

        member this.DeletedLinkedQueuesAsync originQueueName destinationQueueName = delete originQueueName destinationQueueName

type MongoQueueProvider(config: AppConfiguration, logFactory: ILoggerFactory, linkedQueueProvider: ILinkedQueueProvider) as this =

    let queueColNames () =
        Mongo.findCollectionNames config.mongoDbName config.mongoConnection
        |> Array.filter (fun n -> n.StartsWith(MongoQueues.queueNamePrefix))
        |> Array.map (fun n -> n.Substring(MongoQueues.queueNamePrefix.Length))

    let relay = new QueueMessageRelay(this :> IQueueProvider) :> IQueueMessageRelay
    let createQueue name = new MongoQueue(config, logFactory, relay, name) :> IQueue

    let queues =
        let result =
            new System.Collections.Concurrent.ConcurrentDictionary<string, IQueue>(StringComparer.OrdinalIgnoreCase)

        queueColNames ()
        |> Array.iter (fun n -> result.GetOrAdd(n, createQueue) |> ignore)

        result

    let getQueue queueName =
        queues.GetOrAdd(queueName, createQueue)

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

        member this.DeleteQueueAsync(queueName) = 
            task {
                let! r = deleteQueue queueName

                let! linksTo = linkedQueueProvider.GetLinkedQueuesAsync queueName
                let! linksFrom = linkedQueueProvider.GetLinkedToQueuesAsync queueName

                let deletionsTo = linksTo |> Seq.map (fun n -> linkedQueueProvider.DeletedLinkedQueuesAsync queueName n)
                let deletionsFrom = linksFrom |> Seq.map (fun n -> linkedQueueProvider.DeletedLinkedQueuesAsync n queueName)
                let deletions = deletionsTo |> Seq.append deletionsFrom |> Array.ofSeq

                let! rs = System.Threading.Tasks.Task.WhenAll deletions
                                
                return r
            }
    
        member this.LinkQueuesAsync originQueueName destinationQueueName = 
            task {
                let! r = linkedQueueProvider.LinkQueuesAsync originQueueName destinationQueueName 

                if r then   
                    [ originQueueName; destinationQueueName ] |> List.map getQueue |> ignore

                return r
            }
                   
        member this.GetLinkedQueues queueName = 
            task {
                let! names = linkedQueueProvider.GetLinkedQueuesAsync queueName
                return names |> List.map getQueue                
            }
            
        member this.DeleteLinkedQueuesAsync originQueueName destinationQueueName = linkedQueueProvider.DeletedLinkedQueuesAsync originQueueName destinationQueueName 

