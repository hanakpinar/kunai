using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Kunai.Api.Common.Email;

namespace Kunai.Api.IntegrationTests.Infrastructure;

/// <summary>Captures emails instead of sending them, so tests can follow confirmation and reset links.</summary>
public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> SentTo(string address) =>
        _sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Returns the userId and token from the last link of the given kind sent to the address.</summary>
    public (Guid UserId, string Token) LastLink(string address, string path)
    {
        var message = SentTo(address).Last(m => m.TextBody.Contains(path, StringComparison.Ordinal));
        var match = LinkPattern().Match(message.TextBody);
        return (Guid.Parse(match.Groups["user"].Value), match.Groups["token"].Value);
    }

    [GeneratedRegex(@"userId=(?<user>[0-9a-f-]{36})&token=(?<token>[A-Za-z0-9_\-]+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkPattern();
}
