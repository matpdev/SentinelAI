namespace Sentinel.Core.Abstractions;

public interface IPiiRedactionRule
{
    string Apply(string input);
}

public interface IPiiSanitizer
{
    string Sanitize(string? input);
}
