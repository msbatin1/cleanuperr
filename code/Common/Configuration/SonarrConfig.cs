namespace Common.Configuration;

public sealed class SonarrConfig
{
    public required List<SonarrInstance> Instances { get; set; }
}