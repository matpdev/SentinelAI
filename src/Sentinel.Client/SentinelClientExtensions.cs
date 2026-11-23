using Microsoft.Extensions.DependencyInjection;

namespace Sentinel.Client;

public static class SentinelClientExtensions
{
    internal const string HttpClientName = "SentinelIngest";

    public static IServiceCollection AddSentinelClient(
        this IServiceCollection services,
        Action<SentinelClientOptions> configure)
    {
        var options = new SentinelClientOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton(_ => new SentinelEventQueue(options.QueueCapacity));
        services.AddHttpClient(HttpClientName, client =>
        {
            client.BaseAddress = new Uri(options.IngestApiUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHostedService<SentinelBackgroundSender>();
        services.AddProblemDetails();
        services.AddExceptionHandler<SentinelExceptionHandler>();

        return services;
    }
}
