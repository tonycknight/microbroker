namespace microbroker

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

type Startup() =
    
    member _.ConfigureServices(services: IServiceCollection) =
        services
        |> ApiStartup.addApi
        |> ignore

    member _.Configure (app: IApplicationBuilder) (env: IHostEnvironment) (loggerFactory: ILoggerFactory) =
        app.UseHttpLogging()
           .UseGiraffe(ApiRoutes.webApp app.ApplicationServices)

module Program =
    [<EntryPoint>]
    let main argv =
        let host =
            Host
                .CreateDefaultBuilder()
                .ConfigureWebHostDefaults(fun whb ->
                    whb
                        .UseStartup<Startup>()
                        .ConfigureAppConfiguration(ApiStartup.configSource argv >> ignore)
                        .UseUrls("http://+:8080")
                    |> ignore)

                .Build()

        host.Run()

        0 // Exit code

