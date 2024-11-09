namespace Domain.Sonarr.Queue;

public record Record(
    int SeriesId,
    int EpisodeId,
    int SeasonNumber,
    IReadOnlyList<Language> Languages,
    IReadOnlyList<CustomFormat> CustomFormats,
    int CustomFormatScore,
    int Size,
    string Title,
    int Sizeleft,
    string Timeleft,
    DateTime EstimatedCompletionTime,
    DateTime Added,
    string Status,
    string TrackedDownloadStatus,
    string TrackedDownloadState,
    IReadOnlyList<StatusMessage> StatusMessages,
    string DownloadId,
    string Protocol,
    string DownloadClient,
    bool DownloadClientHasPostImportCategory,
    string Indexer,
    string OutputPath,
    bool EpisodeHasFile,
    int Id
);