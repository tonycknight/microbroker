namespace microbroker

open System
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection

[<CLIMutable>]
type AppConfiguration =
    { hostUrls: string
      mongoDbName: string
      mongoConnection: string }

module Configuration =
    let create (sp: System.IServiceProvider) =
        let config =
            sp
                .GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
                .Get<AppConfiguration>()

        config
