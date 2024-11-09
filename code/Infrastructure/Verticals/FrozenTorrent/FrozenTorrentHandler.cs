using Common.Configuration;
using Domain.Sonarr.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using QBittorrent.Client;

namespace Infrastructure.Verticals.FrozenTorrent;

public sealed class FrozenTorrentHandler
{
    private readonly ILogger<FrozenTorrentHandler> _logger;
    private readonly QBitConfig _qBitConfig;
    private readonly SonarrConfig _sonarrConfig;
    private readonly HttpClient _httpClient;
    
    private const string SonarListUriTemplate = "/api/v3/queue?page={0}&pageSize=200&sortKey=timeleft";
    private const string SonarDeleteUriTemplate = "/api/v3/queue/{0}?removeFromClient=true&blocklist=true&skipRedownload=true&changeCategory=false";

    public FrozenTorrentHandler(
        ILogger<FrozenTorrentHandler> logger,
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

            do
            {
                UriBuilder uriBuilder = new UriBuilder(sonarrInstance.Url);
                uriBuilder.Path = string.Format(SonarListUriTemplate, page);

                HttpRequestMessage sonarrRequest = new(HttpMethod.Get, uriBuilder.Uri);
                sonarrRequest.Headers.Add("x-api-key", sonarrInstance.ApiKey);

                HttpResponseMessage response = await _httpClient.SendAsync(sonarrRequest);
                response.EnsureSuccessStatusCode();

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
                        // TODO log skip
                        continue;
                    }
        
                    // delete and block from sonarr
                    uriBuilder = new(sonarrInstance.Url);
                    uriBuilder.Path = string.Format(SonarDeleteUriTemplate, record.Id);
                    
                    sonarrRequest = new(HttpMethod.Delete, uriBuilder.Uri);
                    sonarrRequest.Headers.Add("x-api-key", sonarrInstance.ApiKey);
        
                    response = await _httpClient.SendAsync(sonarrRequest);
                    response.EnsureSuccessStatusCode();
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