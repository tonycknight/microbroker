namespace Microbroker.Client.Tests.Integration

open System
open System.Net.Http
open Microsoft.Extensions.Logging
open Microbroker.Client

module internal Factories =

    let apiBaseUrl () =
        System.Environment.GetEnvironmentVariable "MICROBROKER_CLIENT_INTEGRATION_TEST_TARGETURL"
        |> Option.ofNull
        |> Option.defaultValue "http://localhost:8080/"

    let http () =
        let httpClient = new HttpClient()
        fun () -> httpClient

    let log () =
        NSubstitute.Substitute.For<ILoggerFactory>()

    let internalClient client =
        client () |> InternalHttpClient :> IHttpClient

    let config url =
        { MicrobrokerConfiguration.brokerBaseUrl = url
          throttleMaxTime = TimeSpan.FromSeconds 1. }

    let proxy baseUrl =
        let ihc = http () |> internalClient
        let config = config baseUrl
        let log = log ()
        new MicrobrokerProxy(config, ihc, log) :> IMicrobrokerProxy

    let queueName () =
        $"integration_test_queue_{Guid.NewGuid().ToString()}"

    let msg () =
        MicrobrokerMessages.create ()
        |> MicrobrokerMessages.content $"here I am {Guid.NewGuid().ToString()}"
        |> MicrobrokerMessages.messageType $"message type {Guid.NewGuid().ToString()}"
