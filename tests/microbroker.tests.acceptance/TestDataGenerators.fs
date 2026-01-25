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

    let invalidQueueNames =
        let filter (s: string) =
            s.Length > 0
            && (s.Contains('#') |> not)
            && (s.Contains('?') |> not)
            && (s.Contains('/') |> not)
            && s <> "."

        ArbMap.defaults
        |> ArbMap.arbitrary<string>
        |> Arb.filter filter
        |> Arb.filter (WebApiValidation.isValidQueueName >> not)

    let validQueueNames =

        ArbMap.defaults
        |> ArbMap.arbitrary<Guid>
        |> Arb.toGen
        |> Gen.map (fun g -> $"test-queue-{g}".ToLowerInvariant())
        |> Arb.fromGen

    type AlphaNumericString =

        static member Generate() =
            ArbMap.defaults |> ArbMap.arbitrary<string> |> Arb.filter isValidString

    type QueueMessages =

        static member Generate() =

            let contents =
                ArbMap.defaults
                |> ArbMap.arbitrary<Guid>
                |> Arb.toGen
                |> Gen.map (fun g -> $"message-content-{g}")

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

    type MicrobrokerMessages =
        static member Generate() =
            QueueMessages.Generate().Generator
            |> Gen.map (fun msg ->
                { Microbroker.Client.MicrobrokerMessage.content = msg.content
                  Microbroker.Client.MicrobrokerMessage.messageType = msg.messageType
                  Microbroker.Client.MicrobrokerMessage.created = msg.created
                  Microbroker.Client.MicrobrokerMessage.active = msg.active
                  Microbroker.Client.MicrobrokerMessage.expiry = msg.expiry })
            |> Arb.fromGen
