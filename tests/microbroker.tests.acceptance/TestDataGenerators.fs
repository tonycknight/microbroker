namespace microbroker.tests.acceptance

open System
open FsCheck.FSharp
open microbroker

module MessageGenerators =

    let toJson (message: QueueMessage) =
        Newtonsoft.Json.JsonConvert.SerializeObject message

    let toJsonArray (message: QueueMessage[]) =
        Newtonsoft.Json.JsonConvert.SerializeObject message

    let fromJson (json: string) =
        Newtonsoft.Json.JsonConvert.DeserializeObject<QueueMessage>(json)

module QueueInfoGenerators =
    let fromJson (json: string) =
        Newtonsoft.Json.JsonConvert.DeserializeObject<QueueInfo>(json)

    let fromJsonArray (json: string) =
        Newtonsoft.Json.JsonConvert.DeserializeObject<QueueInfo[]>(json)

module Arbitraries =
    let isAlphaNumeric (value: string) =
        value |> Seq.forall (Char.IsLetter ||>> Char.IsNumber)

    let isNotNullOrEmpty = String.IsNullOrEmpty >> not

    let isValidString = isNotNullOrEmpty &&>> isAlphaNumeric

    type AlphaNumericString =

        static member Generate() =
            ArbMap.defaults |> ArbMap.arbitrary<string> |> Arb.filter isValidString

    type QueueMessages =

        static member Generate() =
            let contents =
                AlphaNumericString.Generate().Generator |> Gen.map (fun s -> $"Content{s}")

            let messageTypes =
                AlphaNumericString.Generate().Generator |> Gen.map (fun s -> $"MessageType{s}")

            let now = DateTimeOffset.UtcNow

            contents
            |> Gen.zip messageTypes
            |> Gen.map (fun (mt, c) ->
                { QueueMessage.messageType = mt
                  content = c
                  created = now
                  expiry = now.AddHours 1
                  active = now.AddHours -1 })
            |> Arb.fromGen
