using Infrastructure.Verticals.FrozenTorrent;
using Quartz;

namespace Executable.Jobs;

[DisallowConcurrentExecution]
public sealed class FrozenTorrentJob : IJob
{
    private ILogger<FrozenTorrentJob> _logger;
    private FrozenTorrentHandler _handler;

    public FrozenTorrentJob(ILogger<FrozenTorrentJob> logger, FrozenTorrentHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _handler.HandleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(FrozenTorrentJob)} failed");
        }
    }
}