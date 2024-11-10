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
            List<int> seriesToBeRefreshed = [];

            do
            {
                Uri sonarrUri = new(sonarrInstance.Url, string.Format(QueueListPathTemplate, page));

                HttpRequestMessage sonarrRequest = new(HttpMethod.Get, sonarrUri);
                sonarrRequest.Headers.Add("x-api-key", sonarrInstance.ApiKey);

                HttpResponseMessage response = await _httpClient.SendAsync(sonarrRequest);

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
                    throw new Exception($"Failed to process response body:{responseBody}");
                }
                
                foreach (Record record in queueResponse.Records)
                {
                    var torrent = (await qBitClient.GetTorrentListAsync(new TorrentListQuery { Hashes = [record.DownloadId] }))
                        .FirstOrDefault();

                    if (torrent is not { CompletionOn: not null, Downloaded: null or 0 })
                    {
                        _logger.LogInformation("skip | {torrent}", record.Title);
                        continue;
                    }
                    
                    seriesToBeRefreshed.Add(record.SeriesId);
                    
                    sonarrUri = new(sonarrInstance.Url, string.Format(QueueDeletePathTemplate, record.Id));
                    sonarrRequest = new(HttpMethod.Delete, sonarrUri);
                    sonarrRequest.Headers.Add("x-api-key", sonarrInstance.ApiKey);
        
                    response = await _httpClient.SendAsync(sonarrRequest);

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

                foreach (int id in seriesToBeRefreshed)
                {
                    sonarrUri = new(sonarrInstance.Url, SonarrCommandUriPath);
                    sonarrRequest = new(HttpMethod.Post, sonarrUri);
                    sonarrRequest.Content = new StringContent(
                        SearchCommandPayloadTemplate.Replace("{0}", id.ToString()),
                        Encoding.UTF8,
                        "application/json"
                    );
                    sonarrRequest.Headers.Add("x-api-key", sonarrInstance.ApiKey);
                    
                    response = await _httpClient.SendAsync(sonarrRequest);

                    try
                    {
                        response.EnsureSuccessStatusCode();
                    }
                    catch
                    {
                        _logger.LogError("series search failed | series id: {id}", id);
                        throw;
                    }
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
}