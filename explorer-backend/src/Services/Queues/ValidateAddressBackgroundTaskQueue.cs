using System.Threading.Channels;
using Microsoft.Extensions.Options;
using ExplorerBackend.Configs;

namespace ExplorerBackend.Services.Queues;

public class ValidateAddressBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _queue;
    private readonly IOptionsMonitor<ExplorerConfig> _explorerConfig;

    public ValidateAddressBackgroundTaskQueue(IOptionsMonitor<ExplorerConfig> explorerConfig)
    {
        _explorerConfig = explorerConfig;
        BoundedChannelOptions options = new(_explorerConfig.CurrentValue.ValidateAddressQueue?.Capacity ?? 10)
        {
            FullMode = (BoundedChannelFullMode)(_explorerConfig.CurrentValue.ValidateAddressQueue?.Mode ?? 0)
        };
        _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);
    }

    public async ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
    {
        Func<CancellationToken, ValueTask>? workItem = await _queue.Reader.ReadAsync(cancellationToken);

        return workItem;
    }
}