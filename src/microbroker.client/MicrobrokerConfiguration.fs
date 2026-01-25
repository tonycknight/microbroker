namespace Microbroker.Client

open System

type MicrobrokerConfiguration =
    { brokerBaseUrl: string
      throttleMaxTime: TimeSpan }
