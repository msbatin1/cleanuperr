using Infrastructure.Verticals.BlockedTorrent;
using Quartz;

namespace Executable.Jobs;

[DisallowConcurrentExecution]
public sealed class BlockedTorrentJob : IJob
{
    private ILogger<BlockedTorrentJob> _logger;
    private BlockedTorrentHandler _handler;

    public BlockedTorrentJob(ILogger<BlockedTorrentJob> logger, BlockedTorrentHandler handler)
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
            _logger.LogError(ex, $"{nameof(BlockedTorrentJob)} failed");
        }
    }
}