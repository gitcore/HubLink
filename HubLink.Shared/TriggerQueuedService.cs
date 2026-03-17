namespace HubLink.Shared;

public interface ITriggerQueue<T> {
    string Tag { get; }
    Task<T> DequeueAsync(CancellationToken cancellationToken);
}

public abstract class TriggerQueuedService<T1, T2>(
    ILogger logger,
    T1 queue) : BackgroundService where T1 : ITriggerQueue<T2>
{

    protected virtual async Task OnInitializeAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task OnDestroyAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
    }

    protected virtual async Task OnTriggerFiredAsync(T2 item, CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
    }

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            $"{queue.Tag} Queued Hosted Service is running.{Environment.NewLine}");

        await OnInitializeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var trigger = await queue.DequeueAsync(stoppingToken);

            try
            {
                if (trigger != null)
                {
                    await OnTriggerFiredAsync(trigger, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error occurred executing {WorkItem}.", nameof(trigger));
            }
        }

        await OnDestroyAsync(stoppingToken);
    }
}
