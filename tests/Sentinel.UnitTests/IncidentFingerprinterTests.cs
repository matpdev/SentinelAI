using Sentinel.Shared.Diagnostics;
using Sentinel.Shared.Models;

namespace Sentinel.UnitTests;

public class IncidentFingerprinterTests
{
    [Fact]
    public void Compute_ShouldBeDeterministic_ForSameIncidentAcrossDifferentTraceIds()
    {
        var evt1 = new IncidentEvent(
            ServiceName: "Orders.Api",
            Environment: "Production",
            ExceptionType: "Npgsql.NpgsqlException",
            Message: "Timeout expired for order 123",
            StackTrace: "System.Exception\n   at Orders.Repository.GetAsync(Int32 id) in Repo.cs:line 42",
            Endpoint: "/api/orders/123",
            TraceId: "trace-111");

        var evt2 = evt1 with { Message = "Timeout expired for order 999", TraceId = "trace-222" };

        var fp1 = IncidentFingerprinter.Compute(evt1);
        var fp2 = IncidentFingerprinter.Compute(evt2);

        Assert.Equal(fp1, fp2);
        Assert.Equal(64, fp1.Length);
    }

    [Fact]
    public void Compute_ShouldDiffer_WhenEndpointDiffers()
    {
        var evt1 = new IncidentEvent("Orders.Api", "Prod", "NullReferenceException", "err", Endpoint: "/api/a");
        var evt2 = evt1 with { Endpoint = "/api/b" };

        Assert.NotEqual(IncidentFingerprinter.Compute(evt1), IncidentFingerprinter.Compute(evt2));
    }
}
