namespace microbroker.tests.acceptance

open System
open FsCheck.FSharp
open microbroker

module MessageGenerators =
    let message messageType content =
        { QueueMessage.messageType = messageType
          QueueMessage.content = content
          QueueMessage.created = DateTimeOffset.UtcNow
          QueueMessage.active = DateTimeOffset.UtcNow
          QueueMessage.expiry = DateTimeOffset.UtcNow.AddDays 1 }

    let toJson (message: QueueMessage) =
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
