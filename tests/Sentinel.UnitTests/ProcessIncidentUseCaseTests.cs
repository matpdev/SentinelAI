using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentinel.Core.Abstractions;
using Sentinel.Core.Security;
using Sentinel.Core.UseCases;
using Sentinel.Shared.Models;

namespace Sentinel.UnitTests;

public partial class ProcessIncidentUseCaseTests
{
    private sealed partial class CnpjRedactionRule : IPiiRedactionRule
    {
        [GeneratedRegex(@"\b\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}\b")]
        private static partial Regex Pattern();

        public string Apply(string input) => Pattern().Replace(input, "[CNPJ_REDACTED]");
    }

    [Fact]
    public void CompositePiiSanitizer_ShouldSupportCustomPluggableRules()
    {
        var sanitizer = new CompositePiiSanitizer([
            new EmailRedactionRule(),
            new CnpjRedactionRule()
        ]);

        var result = sanitizer.Sanitize("Error for corp@test.com CNPJ 12.345.678/0001-99");

        Assert.Equal("Error for [EMAIL_REDACTED] CNPJ [CNPJ_REDACTED]", result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDiagnoseAndNotifyAllNotifiers_WhenIncidentIsNew()
    {
        var repo = Substitute.For<IIncidentRepository>();
        var incidentId = Guid.NewGuid();
        repo.UpsertIncidentAndRecordEventAsync(
                Arg.Any<string>(), Arg.Any<IncidentEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new UpsertIncidentResult(incidentId, IsNewIncident: true, OccurrenceCount: 1));

        var diagService = Substitute.For<IIncidentDiagnosticService>();
        var expectedDiag = new IncidentDiagnosis("DB pool exhausted", "High", "Orders.Api", "Increase pool size");
        diagService.DiagnoseAsync(Arg.Any<IncidentEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedDiag);

        var notifier1 = Substitute.For<IIncidentNotifier>();
        var notifier2 = Substitute.For<IIncidentNotifier>();

        var sut = new ProcessIncidentUseCase(
            new CompositePiiSanitizer([new EmailRedactionRule()]),
            repo,
            diagService,
            [notifier1, notifier2],
            NullLogger<ProcessIncidentUseCase>.Instance);

        var evt = new IncidentEvent("Orders.Api", "Prod", "NpgsqlException", "Timeout for a@b.com");
        var result = await sut.ExecuteAsync(evt, CancellationToken.None);

        Assert.True(result.IsNewIncident);
        Assert.Equal(expectedDiag, result.Diagnosis);
        await repo.Received(1).SaveDiagnosisAsync(incidentId, expectedDiag, Arg.Any<CancellationToken>());
        await notifier1.Received(1).NotifyAsync(incidentId, evt, expectedDiag, Arg.Any<CancellationToken>());
        await notifier2.Received(1).NotifyAsync(incidentId, evt, expectedDiag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipAiAndNotifiers_WhenIncidentIsDeduplicated()
    {
        var repo = Substitute.For<IIncidentRepository>();
        var incidentId = Guid.NewGuid();
        repo.UpsertIncidentAndRecordEventAsync(
                Arg.Any<string>(), Arg.Any<IncidentEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new UpsertIncidentResult(incidentId, IsNewIncident: false, OccurrenceCount: 5));

        var diagService = Substitute.For<IIncidentDiagnosticService>();
        var notifier = Substitute.For<IIncidentNotifier>();

        var sut = new ProcessIncidentUseCase(
            new CompositePiiSanitizer([]),
            repo,
            diagService,
            [notifier],
            NullLogger<ProcessIncidentUseCase>.Instance);

        var evt = new IncidentEvent("Orders.Api", "Prod", "NpgsqlException", "Timeout");
        var result = await sut.ExecuteAsync(evt, CancellationToken.None);

        Assert.False(result.IsNewIncident);
        Assert.Equal(5, result.OccurrenceCount);
        Assert.Null(result.Diagnosis);
        await diagService.DidNotReceive().DiagnoseAsync(Arg.Any<IncidentEvent>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await notifier.DidNotReceive().NotifyAsync(Arg.Any<Guid>(), Arg.Any<IncidentEvent>(), Arg.Any<IncidentDiagnosis>(), Arg.Any<CancellationToken>());
    }
}
