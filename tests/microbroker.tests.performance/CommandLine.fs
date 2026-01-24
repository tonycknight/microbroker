namespace microbroker.tests.performance

open CommandLine

type CommandOptions =
    { [<Option("rate", HelpText = "Requests/second", Default = 100)>]
      rate: int32
      [<Option("host", HelpText = "Target API host.", Default = "http://localhost:8080")>]
      host: string
      [<Option("duration", HelpText = "Duration in seconds", Default = 180)>]
      duration: int32 }
