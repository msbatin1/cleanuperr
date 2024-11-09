namespace Common.Configuration;

public sealed class QBitConfig
{
    public required Uri Url { get; set; }
    
    public required string Username { get; set; }
    
    public required string Password { get; set; }
}