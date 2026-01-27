namespace microbroker

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type TtaQueueMonitor(logFactory: ILoggerFactory, queueProvider: IQueueProvider, config: AppConfiguration) =

    inherit BackgroundService()

    let log = logFactory.CreateLogger<TtaQueueMonitor>()
    let scanFrequency = config.ttaScanFrequency

    let moveTtaMessages (cancellationToken: CancellationToken) (name: string) =
        task {
            try
                let! queue = queueProvider.GetQueueAsync name

                let! deletions = queue.MoveTtaMessagesAsync()
                ignore deletions

            with ex ->
                log.LogError(ex, ex.Message)
        }

    let moveMessages (cancellationToken: CancellationToken) =
        task {
            let! names = queueProvider.GetQueueNamesAsync()

            for name in names do
                do! moveTtaMessages cancellationToken name
        }

    override this.ExecuteAsync(cancellationToken: CancellationToken) : Task =
        task {
            log.LogTrace $"TTA Queue Monitor starting with frequency {scanFrequency}..."

            try
                use timer = new PeriodicTimer(scanFrequency)

                let mutable loop = true

                try
                    while loop do
                        let! cont = timer.WaitForNextTickAsync cancellationToken

                        if cont then
                            do! moveMessages cancellationToken

                        loop <- cont

                with
                | :? OperationCanceledException -> log.LogInformation("TTA Queue Monitor stopping...")
                | ex -> log.LogError(ex, "TTA Queue Monitor encountered an error: {0}. Stopping...", ex.Message)

            finally
                log.LogInformation("TTA Queue Monitor stopped.")
        }
