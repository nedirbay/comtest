using System.Threading.Channels;
using ComTest.Infrastructure.Identity;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ComTest.Infrastructure.Messaging;

public sealed record EmailMessage(string To, string Subject, string TextBody)
{
    public override string ToString() => nameof(EmailMessage);
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Sends mail outside the request. Besides keeping requests fast, this removes a timing signal:
/// otherwise "email sent" and "no such user" would take measurably different time to answer.
/// </summary>
public interface IEmailDispatcher
{
    bool TryEnqueue(EmailMessage message);
}

public sealed class InMemoryEmailDispatcher : IEmailDispatcher
{
    private const int Capacity = 1000;

    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public bool TryEnqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
}

public sealed partial class EmailDeliveryService(
    InMemoryEmailDispatcher queue,
    IEmailSender sender,
    ILogger<EmailDeliveryService> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await DeliverAsync(message, stoppingToken);
        }
    }

    private async Task DeliverAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await sender.SendAsync(message, cancellationToken);

                return;
            }
#pragma warning disable CA1031 // One undeliverable message must not stop the delivery loop.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogDeliveryFailed(logger, exception, attempt, MaxAttempts);

                if (attempt < MaxAttempts)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email delivery failed on attempt {Attempt} of {MaxAttempts}")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, int attempt, int maxAttempts);
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            settings.Host!,
            settings.Port,
            settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}

/// <summary>
/// Development only: prints mail to the console so that links can be followed without an SMTP
/// server. It bypasses the logger on purpose, because log masking would hide the link's token.
/// </summary>
public sealed class ConsoleEmailSender : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await Console.Out.WriteLineAsync($"--- Development email ---\n{message.Subject}\n{message.TextBody}");
    }
}

public interface ISmsSender
{
    /// <returns>False when the message could not be handed to a provider.</returns>
    Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken);
}

/// <summary>Development only: prints the code to the log. Never registered in other environments.</summary>
public sealed partial class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        LogSms(logger, message);

        return Task.FromResult(true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Development SMS: {Body}")]
    private static partial void LogSms(ILogger logger, string body);
}

/// <summary>Used until a real SMS provider is configured, so that codes are never lost silently.</summary>
public sealed class UnconfiguredSmsSender : ISmsSender
{
    public Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
