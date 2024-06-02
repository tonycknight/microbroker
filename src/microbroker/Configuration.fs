namespace microbroker

open System
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection

[<CLIMutable>]
type AppConfiguration =
    { hostUrls: string
      mongoDbName: string
      mongoConnection: string }

    static member defaultConfig =
        { AppConfiguration.hostUrls = "http://+:8080"
          mongoDbName = ""
          mongoConnection = "" }

module Configuration =
    let create (sp: System.IServiceProvider) =
        let config =
            sp
                .GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
                .Get<AppConfiguration>()

        config
