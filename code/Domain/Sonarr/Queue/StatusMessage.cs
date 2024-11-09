namespace Domain.Sonarr.Queue;

public record StatusMessage(
    string Title,
    IReadOnlyList<string> Messages
);