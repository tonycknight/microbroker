namespace Microbroker.Client.Tests

open System
open System.Net.Http
open Microbroker.Client
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open NSubstitute
open Xunit
open FsUnit

module DependencyInjectionTests =

    let serviceCollection () =
        (new ServiceCollection() :> IServiceCollection)
            .AddSingleton<HttpClient>(new HttpClient())
            .AddSingleton<ILoggerFactory>(Substitute.For<ILoggerFactory>())

    let config () =
        { MicrobrokerConfiguration.brokerBaseUrl = "aaaa"
          throttleMaxTime = TimeSpan.FromSeconds 5. }

    [<Fact>]
    let ``addConfiguration injects instance`` () =
        let config = config ()

        let r =
            serviceCollection ()
            |> DependencyInjection.addConfiguration (fun _ -> config)
            |> _.BuildServiceProvider()
            |> _.GetRequiredService<MicrobrokerConfiguration>()

        r |> should equal config

    [<Fact>]
    let ``addConfiguration injects final instance`` () =
        let config1 = config ()
        let config2 = { config1 with brokerBaseUrl = "zzz" }

        let r =
            serviceCollection ()
            |> DependencyInjection.addConfiguration (fun _ -> config1)
            |> DependencyInjection.addConfiguration (fun _ -> config2)
            |> _.BuildServiceProvider()
            |> _.GetRequiredService<MicrobrokerConfiguration>()

        r |> should equal config2

    [<Fact>]
    let ``addServices injects instance`` () =
        let config = config ()

        let sp =
            serviceCollection ()
            |> DependencyInjection.addConfiguration (fun _ -> config)
            |> DependencyInjection.addServices
            |> _.BuildServiceProvider()

        let proxy = sp.GetService<IMicrobrokerProxy>()

        proxy |> should not' (equal null)
