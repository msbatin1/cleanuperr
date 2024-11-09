namespace Common.Configuration;

public sealed class SonarrInstance
{
    public required Uri Url { get; set; }
    
    public required string ApiKey { get; set; }
}