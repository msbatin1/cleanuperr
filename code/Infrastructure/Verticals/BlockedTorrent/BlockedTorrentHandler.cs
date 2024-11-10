using System.Text;
using Common.Configuration;
using Domain.Sonarr.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using QBittorrent.Client;

namespace Infrastructure.Verticals.BlockedTorrent;

public sealed class BlockedTorrentHandler
{
    private readonly ILogger<BlockedTorrentHandler> _logger;
    private readonly QBitConfig _qBitConfig;
    private readonly SonarrConfig _sonarrConfig;
    private readonly HttpClient _httpClient;
    
    private const string QueueListPathTemplate = "/api/v3/queue?page={0}&pageSize=200&sortKey=timeleft";
    private const string QueueDeletePathTemplate = "/api/v3/queue/{0}?removeFromClient=true&blocklist=true&skipRedownload=true&changeCategory=false";
    private const string SonarrCommandUriPath = "/api/v3/command";
    private const string SearchCommandPayloadTemplate = "{\"name\":\"SeriesSearch\",\"seriesId\":{0}}";
    
    public BlockedTorrentHandler(
        ILogger<BlockedTorrentHandler> logger,
        IOptions<QBitConfig> qBitConfig,
        IOptions<SonarrConfig> sonarrConfig,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _qBitConfig = qBitConfig.Value;
        _sonarrConfig = sonarrConfig.Value;
        _httpClient = httpClientFactory.CreateClient();
    }
    
    public async Task HandleAsync()
    {
        QBittorrentClient qBitClient = new(_qBitConfig.Url);
        
        await qBitClient.LoginAsync(_qBitConfig.Username, _qBitConfig.Password);

        foreach (SonarrInstance sonarrInstance in _sonarrConfig.Instances)
        {
            ushort page = 1;
            int totalRecords = 0;
            int processedRecords = 0;
            HashSet<int> seriesToBeRefreshed = [];

            do
            {
                QueueListResponse queueResponse = await ListQueuedTorrentsAsync(sonarrInstance, page);
                
                foreach (Record record in queueResponse.Records)
                {
                    var torrent = (await qBitClient.GetTorrentListAsync(new TorrentListQuery { Hashes = [record.DownloadId] }))
                        .FirstOrDefault();

                    if (torrent is not { CompletionOn: not null, Downloaded: null or 0 })
                    {
                        _logger.LogInformation("skip | {torrent}", record.Title);
                        return;
                    }
                    
                    seriesToBeRefreshed.Add(record.SeriesId);

                    await DeleteTorrentFromQueueAsync(sonarrInstance, record);
                }

                foreach (int id in seriesToBeRefreshed)
                {
                    await RefreshSeriesAsync(sonarrInstance, id);
                }
                
                if (queueResponse.Records.Count is 0)
                {
                    break;
                }

                if (totalRecords is 0)
                {
                    totalRecords = queueResponse.TotalRecords;
                }
    
                processedRecords += queueResponse.Records.Count;

                if (processedRecords >= totalRecords)
                {
                    break;
                }
    
                page++;
            } while (processedRecords < totalRecords);
        }
    }

    private async Task<QueueListResponse> ListQueuedTorrentsAsync(SonarrInstance sonarrInstance, int page)
    {
        Uri sonarrUri = new(sonarrInstance.Url, string.Format(QueueListPathTemplate, page));

        using HttpRequestMessage sonarrRequest = new(HttpMethod.Get, sonarrUri);
        sonarrRequest.Headers.Add("x-api-key", sonarrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(sonarrRequest);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            _logger.LogError("queue list failed | {uri}", sonarrUri);
            throw;
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        QueueListResponse? queueResponse = JsonConvert.DeserializeObject<QueueListResponse>(responseBody);

        if (queueResponse is null)
        {
            throw new Exception($"unrecognized response | {responseBody}");
        }

        return queueResponse;
    }

    private async Task DeleteTorrentFromQueueAsync(SonarrInstance sonarrInstance, Record record)
    {
        Uri sonarrUri = new(sonarrInstance.Url, string.Format(QueueDeletePathTemplate, record.Id));
        using HttpRequestMessage sonarrRequest = new(HttpMethod.Delete, sonarrUri);
        sonarrRequest.Headers.Add("x-api-key", sonarrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(sonarrRequest);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            _logger.LogError("queue delete failed | {uri}", sonarrUri);
            throw;
        }
    }

    private async Task RefreshSeriesAsync(SonarrInstance sonarrInstance, int seriesId)
    {
        Uri sonarrUri = new(sonarrInstance.Url, SonarrCommandUriPath);
        using HttpRequestMessage sonarrRequest = new(HttpMethod.Post, sonarrUri);
        sonarrRequest.Content = new StringContent(
            SearchCommandPayloadTemplate.Replace("{0}", seriesId.ToString()),
            Encoding.UTF8,
            "application/json"
        );
        sonarrRequest.Headers.Add("x-api-key", sonarrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(sonarrRequest);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            _logger.LogError("series search failed | series id: {id}", seriesId);
            throw;
        }
    }
}