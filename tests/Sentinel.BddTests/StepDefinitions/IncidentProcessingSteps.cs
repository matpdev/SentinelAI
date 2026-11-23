using Reqnroll;
using Sentinel.Shared.Diagnostics;
using Sentinel.Shared.Models;
using Sentinel.Shared.Security;

namespace Sentinel.BddTests.StepDefinitions;

[Binding]
public sealed class IncidentProcessingSteps
{
    private readonly List<IncidentEvent> _events = [];
    private string _fp1 = string.Empty;
    private string _fp2 = string.Empty;
    private string _rawMessage = string.Empty;
    private string _sanitizedMessage = string.Empty;

    [Given(@"an error event from service ""(.*)"" on endpoint ""(.*)"" with exception ""(.*)"" and traceId ""(.*)""")]
    public void GivenAnErrorEvent(string service, string endpoint, string exceptionType, string traceId)
    {
        _events.Add(new IncidentEvent(service, "Production", exceptionType, "Failure", Endpoint: endpoint, TraceId: traceId));
    }

    [Given(@"a second error event from service ""(.*)"" on endpoint ""(.*)"" with exception ""(.*)"" and traceId ""(.*)""")]
    public void GivenASecondErrorEvent(string service, string endpoint, string exceptionType, string traceId)
    {
        _events.Add(new IncidentEvent(service, "Production", exceptionType, "Failure", Endpoint: endpoint, TraceId: traceId));
    }

    [When(@"I compute the fingerprints for both events")]
    public void WhenIComputeTheFingerprints()
    {
        _fp1 = IncidentFingerprinter.Compute(_events[0]);
        _fp2 = IncidentFingerprinter.Compute(_events[1]);
    }

    [Then(@"both fingerprints must be identical")]
    public void ThenBothFingerprintsMustBeIdentical()
    {
        Assert.Equal(_fp1, _fp2);
    }

    [Given(@"a raw error message containing ""(.*)""")]
    public void GivenARawErrorMessage(string rawMessage)
    {
        _rawMessage = rawMessage;
    }

    [When(@"the PII sanitizer processes the message")]
    public void WhenThePiiSanitizerProcessesTheMessage()
    {
        _sanitizedMessage = PiiSanitizer.Sanitize(_rawMessage);
    }

    [Then(@"the sanitized message must not contain ""(.*)""")]
    public void ThenTheSanitizedMessageMustNotContain(string sensitiveValue)
    {
        Assert.DoesNotContain(sensitiveValue, _sanitizedMessage);
    }
}
