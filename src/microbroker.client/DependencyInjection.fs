namespace Microbroker.Client

open System
open Microsoft.Extensions.DependencyInjection

module DependencyInjection =
    let addConfiguration (config: IServiceProvider -> MicrobrokerConfiguration) (sc: IServiceCollection) =
        let sp = sc.BuildServiceProvider()
        sc.AddSingleton<MicrobrokerConfiguration>(config sp)

    let addServices (sc: IServiceCollection) =
        sc.AddSingleton<IHttpClient, InternalHttpClient>().AddSingleton<IMicrobrokerProxy, MicrobrokerProxy>()
