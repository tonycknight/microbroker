namespace microbroker

open System
open MongoDB.Bson
open MongoDB.Driver
open MongoDB.Bson.Serialization

module MongoBson =

    let objectId () = ObjectId.GenerateNewId()

    let ofJson (json: string) =
        BsonSerializer.Deserialize<BsonDocument>(json)

    let ofObject (value) =
        value |> Newtonsoft.Json.JsonConvert.SerializeObject |> ofJson

    let toObject<'a> (doc: BsonDocument) =
        MongoDB.Bson.Serialization.BsonSerializer.Deserialize<'a>(doc)

    let setDocId (id) (doc: BsonDocument) =
        let existingId = doc.Elements |> Seq.filter (fun e -> e.Name = "_id") |> Seq.tryHead

        match existingId with
        | None ->
            doc["_id"] <- id
            doc
        | _ -> doc

    let getDocId (bson: BsonDocument) =
        bson.Elements |> Seq.filter (fun e -> e.Name = "_id") |> Seq.head

    let getObjectId (bson: BsonDocument) =
        bson |> getDocId |> (fun id -> id.Value.AsObjectId)

    let getId (bson: BsonDocument) =
        bson |> getDocId |> (fun id -> id.Value.AsString)

module Mongo =
    let private idFilter id = sprintf @"{ _id: ""%s"" }" id

    let setDbConnection dbName (connectionString: string) =
        match dbName with
        | Strings.NullOrWhitespace _ -> connectionString
        | x -> $"""{connectionString |> Strings.appendIfMissing "/"}{dbName}"""

    let initDb dbName (connection: string) =
        let client = MongoClient(connection)
        let db = client.GetDatabase(dbName)

        try
            new MongoDB.Driver.BsonDocumentCommand<Object>(BsonDocument.Parse("{ping:1}"))
            |> db.RunCommand
            |> ignore

            db
        with :? System.TimeoutException as ex ->
            raise (
                new ApplicationException "Cannot connect to DB. Check the server name, credentials & firewalls are correct."
            )

    let setIndex (path: string) (collection: IMongoCollection<'a>) =
        let json = sprintf "{'%s': 1 }" path
        let def = IndexKeysDefinition<'a>.op_Implicit (json)
        let model = CreateIndexModel<'a>(def)
        let r = collection.Indexes.CreateOne(model)

        collection

    let getCollection colName (db: IMongoDatabase) = db.GetCollection(colName)

    let initCollection indexPath dbName collectionName connectionString =
        let col =
            connectionString
            |> setDbConnection dbName
            |> initDb dbName
            |> getCollection collectionName

        if indexPath <> "" then col |> setIndex indexPath else col

    let deleteCollection (collection: IMongoCollection<BsonDocument>) =
        let db = collection.Database
        let name = collection.CollectionNamespace.CollectionName
        db.DropCollection(name)

    let findCollectionNames dbName connectionString =
        use colNames =
            connectionString
            |> setDbConnection dbName
            |> initDb dbName
            |> (fun db -> db.ListCollectionNames())

        colNames.ToEnumerable() |> Array.ofSeq

    let upsert (collection: IMongoCollection<BsonDocument>) (doc: BsonDocument) =
        let opts = ReplaceOptions()
        opts.IsUpsert <- true

        let filter =
            doc
            |> MongoBson.getId
            |> idFilter
            |> MongoBson.ofJson
            |> FilterDefinition.op_Implicit

        collection.ReplaceOneAsync(filter, doc, opts)

    let query<'a> (collection: IMongoCollection<BsonDocument>) =
        collection.AsQueryable<BsonDocument>() |> Seq.map MongoBson.toObject<'a>

    let estimatedCount (collection: IMongoCollection<BsonDocument>) =
        collection.EstimatedDocumentCountAsync()

    let pullSingletonFromQueue<'a> (collection: IMongoCollection<BsonDocument>) =
        task {
            let filter = new MongoDB.Bson.BsonDocument() |> FilterDefinition.op_Implicit
            let opts = new FindOneAndDeleteOptions<MongoDB.Bson.BsonDocument>()

            let! r = collection.FindOneAndDeleteAsync<MongoDB.Bson.BsonDocument>(filter, opts)

            return
                match Object.ReferenceEquals(r, null) with
                | true -> None
                | _ -> r |> MongoBson.toObject<'a> |> Some
        }

    let pushToQueue<'a> (collection: IMongoCollection<BsonDocument>) (values: seq<'a>) =
        task {
            let values = values |> Seq.map MongoBson.ofObject |> Array.ofSeq

            if values.Length > 0 then
                let opts = new InsertManyOptions()

                do! collection.InsertManyAsync(values, opts)
        }

    let pullFromTta<'a> (collection: IMongoCollection<BsonDocument>) (active: DateTimeOffset) =
        task {
            let active = active.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00")

            let filter =
                $"{{ 'active':  {{ $lte: '{active}' }} }}"
                |> MongoBson.ofJson
                |> FilterDefinition.op_Implicit

            use! r = collection.FindAsync(filter)

            return r.ToEnumerable() |> Seq.map MongoBson.toObject<'a> |> Array.ofSeq
        }

    let deleteFromQueue<'a> (collection: IMongoCollection<BsonDocument>) (predicate: string) =
        task {
            let filter = predicate |> MongoBson.ofJson |> FilterDefinition.op_Implicit

            let! r = collection.DeleteManyAsync filter

            return r.DeletedCount
        }
