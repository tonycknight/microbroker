namespace microbroker

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

type Startup() =

    member _.ConfigureServices(services: IServiceCollection) = services |> ApiStartup.addApi |> ignore

    member _.Configure (app: IApplicationBuilder) (env: IHostEnvironment) (loggerFactory: ILoggerFactory) =
        app.UseHttpLogging().UseGiraffe(ApiRoutes.webApp app.ApplicationServices)

module Program =
    let hostUrls (config: IConfiguration) =
        match config[nameof Unchecked.defaultof<AppConfiguration>.hostUrls] with
        | null -> AppConfiguration.defaultConfig.hostUrls
        | x -> x

    let config argv =
        (new ConfigurationBuilder() |> ApiStartup.configSource argv).Build()

    [<EntryPoint>]
    let main argv =
        let host =
            Host
                .CreateDefaultBuilder()
                .ConfigureWebHostDefaults(fun whb ->
                    whb
                        .UseStartup<Startup>()
                        .ConfigureAppConfiguration(ApiStartup.configSource argv >> ignore)
                        .UseUrls(argv |> config |> hostUrls)
                    |> ignore)

                .Build()

        host.Run()

        0 // Exit code
