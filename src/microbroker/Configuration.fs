namespace microbroker

open System
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection

[<CLIMutable>]
type AppConfiguration =
    { hostUrls: string
      mongoDbName: string
      mongoConnection: string 
      ttaScanFrequency: TimeSpan }

    static member defaultConfig =
        { AppConfiguration.hostUrls = "http://+:8080"
          mongoDbName = ""
          mongoConnection = ""
          ttaScanFrequency = TimeSpan.FromSeconds 60. }

module Configuration =
    let create (sp: System.IServiceProvider) =
        let config =
            sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>().Get<AppConfiguration>()

        config
