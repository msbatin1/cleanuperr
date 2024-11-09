namespace Domain.Sonarr.Queue;

public record Revision(
    int Version,
    int Real,
    bool IsRepack
);