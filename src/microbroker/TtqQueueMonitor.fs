namespace microbroker

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type TtaQueueMonitor(logFactory: ILoggerFactory, queueProvider: IQueueProvider) =
    
    inherit BackgroundService() 

    let log = logFactory.CreateLogger<TtaQueueMonitor>()
    
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
            log.LogTrace "TTA Queue Monitor starting..."
            try
                let frequency = TimeSpan.FromSeconds 15. // TODO: TimeSpan.FromMinutes(1.) config?
                use timer = new PeriodicTimer(frequency)

                let mutable loop = true                
                try 
                    while loop do
                        do! moveMessages cancellationToken
                        let! x = timer.WaitForNextTickAsync cancellationToken
                        loop <- x
                
                with
                    | :? OperationCanceledException -> log.LogInformation("TTA Queue Monitor stopping...")
                    | ex -> log.LogError(ex, "TTA Queue Monitor encountered an error: {0}. Stopping...", ex.Message)

            finally
                log.LogInformation("TTA Queue Monitor stopped.")
        }