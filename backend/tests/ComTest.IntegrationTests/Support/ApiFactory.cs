using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Asp.Versioning;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Domain.Modules.Catalog;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Infrastructure.Identity.Passwords;
using ComTest.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ComTest.IntegrationTests.Support;

public sealed class ApiFactory(ApiFixture fixture) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailDispatcher>();
            services.AddSingleton<IEmailDispatcher>(fixture.Emails);

            services.RemoveAll<ISmsSender>();
            services.AddSingleton<ISmsSender>(fixture.Sms);

            services.RemoveAll<IBreachedPasswordChecker>();
            services.AddSingleton<IBreachedPasswordChecker>(fixture.BreachedPasswords);

            services.AddSingleton(fixture.DomainEvents);
            services.AddScoped<IDomainEventSubscriber<ProductPublished>, RecordingSubscriber>();

            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
        });
    }
}

/// <summary>What the outbox delivered, and which products should make delivery fail.</summary>
public sealed class DomainEventRecorder
{
    private readonly ConcurrentQueue<(Guid ProductId, Guid? TenantId)> _delivered = new();
    private readonly ConcurrentDictionary<Guid, bool> _failing = new();

    public IReadOnlyList<(Guid ProductId, Guid? TenantId)> Delivered => _delivered.ToList();

    public void FailFor(Guid productId) => _failing[productId] = true;

    public void Record(Guid productId, Guid? tenantId)
    {
        if (_failing.ContainsKey(productId))
        {
            throw new InvalidOperationException("Delivery failed on purpose.");
        }

        _delivered.Enqueue((productId, tenantId));
    }
}

public sealed class RecordingSubscriber(DomainEventRecorder recorder, ITenantContext tenantContext) : IDomainEventSubscriber<ProductPublished>
{
    public Task HandleAsync(ProductPublished domainEvent, CancellationToken cancellationToken)
    {
        recorder.Record(domainEvent.ProductId, tenantContext.TenantId);

        return Task.CompletedTask;
    }
}

public sealed partial class CapturingEmailDispatcher : IEmailDispatcher
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public bool TryEnqueue(EmailMessage message)
    {
        _messages.Enqueue(message);

        return true;
    }

    public IReadOnlyList<EmailMessage> For(string address) =>
        _messages.Where(message => string.Equals(message.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    public (Guid UserId, string Token) ReadLink(string address)
    {
        var match = LinkPattern().Match(For(address)[^1].TextBody);

        Assert.True(match.Success, "The last email has no account link.");

        return (Guid.Parse(match.Groups[1].Value), Uri.UnescapeDataString(match.Groups[2].Value));
    }

    [GeneratedRegex(@"#userId=([0-9a-f-]+)&token=(\S+)")]
    private static partial Regex LinkPattern();
}

public sealed class CapturingSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _lastCodes = new(StringComparer.Ordinal);

    public bool Available { get; set; } = true;

    public string LastCode(string phoneNumber) => _lastCodes[phoneNumber];

    public Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        if (!Available)
        {
            return Task.FromResult(false);
        }

        _lastCodes[phoneNumber] = message;

        return Task.FromResult(true);
    }
}

public sealed class StubBreachedPasswordChecker : IBreachedPasswordChecker
{
    private readonly ConcurrentDictionary<string, bool> _breached = new(StringComparer.Ordinal);

    public void MarkBreached(string password) => _breached[password] = true;

    public Task<bool> IsBreachedAsync(string password, CancellationToken cancellationToken) =>
        Task.FromResult(_breached.ContainsKey(password));
}

/// <summary>Endpoints that exist only in tests, to exercise cross-cutting behaviour.</summary>
[ApiController]
[ApiVersionNeutral]
[Route("test")]
public sealed class TestEndpointsController : ControllerBase
{
    public const string SecretInException = "Host=internal-db;Password=do-not-leak";

    [HttpGet("no-attribute")]
    public IActionResult NoAttribute() => Ok();

    [AllowAnonymous]
    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException(SecretInException);

    [AllowAnonymous]
    [HttpGet("concurrency")]
    public IActionResult Concurrency() => throw new DbUpdateConcurrencyException(SecretInException);

    [AllowAnonymous]
    [HttpGet("slow")]
    public async Task<IActionResult> Slow(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

        return Ok();
    }

    [HttpGet("catalog-write")]
    [HasPermission(Permissions.CatalogWrite)]
    public IActionResult CatalogWrite() => Ok();

    [HttpGet("refund")]
    [HasPermission(Permissions.PaymentsRefund)]
    public IActionResult Refund() => Ok();
}
