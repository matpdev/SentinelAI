namespace Sentinel.Client;

public sealed class SentinelClientOptions
{
    public string ServiceName { get; set; } = "Unknown.Service";
    public string Environment { get; set; } = "Production";
    public string IngestApiUrl { get; set; } = "http://localhost:5000";
    public int QueueCapacity { get; set; } = 1000;
}
