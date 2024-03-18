﻿namespace microbroker

open System
open Giraffe
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

module ApiStartup =
    open Microsoft.AspNetCore.HttpLogging

    let addApiLogging (services: IServiceCollection) =
        services
            .AddLogging()
            .AddHttpLogging(fun lo ->
                lo.LoggingFields <-
                    HttpLoggingFields.Duration
                    ||| HttpLoggingFields.RequestPath
                    ||| HttpLoggingFields.RequestQuery
                    ||| HttpLoggingFields.RequestProtocol
                    ||| HttpLoggingFields.RequestMethod
                    ||| HttpLoggingFields.RequestScheme
                    ||| HttpLoggingFields.ResponseStatusCode

                lo.CombineLogs <- true)


    let addWebFramework (services: IServiceCollection) = services.AddGiraffe()

    let addContentNegotiation (services: IServiceCollection) =
        services.AddSingleton<INegotiationConfig, JsonOnlyNegotiationConfig>()

    let addApiServices (services: IServiceCollection) =
        services
            .AddSingleton<IQueueFactory, QueueFactory>()
            .AddSingleton<IQueueProvider, QueueProvider>()

    let addApiConfig (services: IServiceCollection) =
        let sp = services.BuildServiceProvider()

        let config = Configuration.create sp

        services.AddSingleton<AppConfiguration>(config)

    let addApi<'a when 'a :> IServiceCollection> =
        addApiLogging
        >> addWebFramework
        >> addApiConfig
        >> addContentNegotiation
        >> addApiServices

    let configSource (args: string[]) (whbc: IConfigurationBuilder) =
        let whbc = whbc.AddEnvironmentVariables("microbroker_").AddCommandLine(args)

        let configPath =
            args
            |> Args.getValue "--config="
            |> Option.map (Io.toFullPath >> Io.normalise)
            |> Option.defaultValue ""

        if configPath <> "" then
            if Io.fileExists configPath |> not then
                failwithf $"{configPath} not found."

            whbc.AddJsonFile(configPath, true, false)
        else
            whbc
