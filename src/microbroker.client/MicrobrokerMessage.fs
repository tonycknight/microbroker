namespace Microbroker.Client

open System

[<CLIMutable>]
type MicrobrokerMessage =
    { messageType: string
      content: string
      created: DateTimeOffset
      active: DateTimeOffset
      expiry: DateTimeOffset }

module MicrobrokerMessages =
    let internal fromString (value: string) =
        try
            Newtonsoft.Json.JsonConvert.DeserializeObject<MicrobrokerMessage>(value) |> Some
        with ex ->
            None

    let internal toJsonArray (messages: MicrobrokerMessage[]) =
        Newtonsoft.Json.JsonConvert.SerializeObject(messages)

    let create () =
        let now = Time.now ()

        { MicrobrokerMessage.created = now
          content = ""
          messageType = ""
          active = DateTimeOffset.MinValue
          expiry = DateTimeOffset.MaxValue }

    let active (active: unit -> DateTimeOffset) (message: MicrobrokerMessage) = { message with active = active () }

    let delayed (delay: unit -> TimeSpan) (message: MicrobrokerMessage) =
        let active = Time.now () |> Time.add (delay ())
        { message with active = active }

    let messageType messageType (message: MicrobrokerMessage) =
        { message with
            messageType = messageType }

    let content content (message: MicrobrokerMessage) = { message with content = content }

    let expiry (delay: unit -> TimeSpan) (message: MicrobrokerMessage) =
        let expires = Time.now () |> Time.add (delay ())
        { message with expiry = expires }
