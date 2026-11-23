using Amazon.Runtime;
using Amazon.SQS;
using Sentinel.Diagnostic.Worker;
using Sentinel.Diagnostic.Worker.Data;
using Sentinel.Diagnostic.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    var serviceUrl = builder.Configuration["Sentinel:SqsServiceUrl"];
    if (!string.IsNullOrWhiteSpace(serviceUrl))
    {
        var config = new AmazonSQSConfig { ServiceURL = serviceUrl };
        return new AmazonSQSClient(new BasicAWSCredentials("test", "test"), config);
    }

    return new AmazonSQSClient();
});

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddSingleton(new IncidentRepository(connectionString));
builder.Services.AddSingleton<IIncidentDiagnosticService, FallbackHeuristicDiagnosticService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
