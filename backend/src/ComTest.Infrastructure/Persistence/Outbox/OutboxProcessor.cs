using System.Reflection;
using System.Text.Json;
using ComTest.Application.Abstractions;
using ComTest.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ComTest.Infrastructure.Persistence.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Enabled { get; init; } = true;

    public int BatchSize { get; init; } = 20;

    public int MaxAttempts { get; init; } = 10;

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Known event types by name. Payloads are only ever deserialized into these types, so a
/// tampered row cannot make the processor instantiate an arbitrary class.
/// </summary>
public sealed class DomainEventRegistry
{
    private readonly Dictionary<string, Type> _types;

    public DomainEventRegistry(IEnumerable<Assembly> assemblies)
    {
        _types = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(type))
            .ToDictionary(type => type.FullName!, StringComparer.Ordinal);
    }

    public Type? Find(string name) => _types.GetValueOrDefault(name);
}

public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    DomainEventRegistry registry,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger)
{
    private const int MaxErrorLength = 200;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var batchSize = options.Value.BatchSize;

        // SKIP LOCKED lets several API instances share the work without double delivery.
        var messages = await db.Set<OutboxMessage>()
            .FromSql($"""
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND dead_lettered_at IS NULL AND next_attempt_at <= {now}
                ORDER BY occurred_at
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await DeliverAsync(message, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.Count;
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        try
        {
            var eventType = registry.Find(message.Type)
                ?? throw new InvalidOperationException($"Unknown domain event type '{message.Type}'.");

            var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType)
                ?? throw new InvalidOperationException("The outbox payload is empty.");

            await using var scope = scopeFactory.CreateAsyncScope();

            if (message.TenantId is { } tenantId)
            {
                scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            }

            await DispatchAsync(scope.ServiceProvider, eventType, domainEvent, cancellationToken);

            message.ProcessedAt = now;
            message.LastError = null;
        }
#pragma warning disable CA1031 // One failing event must not block the rest of the batch.
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            RecordFailure(message, exception, now);
        }
    }

    private void RecordFailure(OutboxMessage message, Exception exception, DateTimeOffset now)
    {
        message.Attempts++;

        // The type only: exception messages can contain personal data from the payload.
        var error = exception.GetType().FullName ?? exception.GetType().Name;
        message.LastError = error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];

        if (message.Attempts >= options.Value.MaxAttempts)
        {
            message.DeadLetteredAt = now;
            LogDeadLettered(logger, exception, message.Id, message.Type, message.Attempts);
            return;
        }

        message.NextAttemptAt = now.AddSeconds(Math.Pow(2, message.Attempts));
        LogDeliveryFailed(logger, exception, message.Id, message.Type, message.Attempts);
    }

    private static async Task DispatchAsync(
        IServiceProvider services,
        Type eventType,
        object domainEvent,
        CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventSubscriber<>).MakeGenericType(eventType);
        var handle = handlerType.GetMethod(nameof(IDomainEventSubscriber<IDomainEvent>.HandleAsync))!;

        foreach (var handler in services.GetServices(handlerType))
        {
            // DoNotWrapExceptions keeps the real exception type for the error record.
            await (Task)handle.Invoke(handler, BindingFlags.DoNotWrapExceptions, null, [domainEvent, cancellationToken], null)!;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({MessageType}) failed on attempt {Attempts}")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, Guid messageId, string messageType, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({MessageType}) moved to dead letter after {Attempts} attempts")]
    private static partial void LogDeadLettered(ILogger logger, Exception exception, Guid messageId, string messageType, int attempts);
}

public sealed partial class OutboxBackgroundService(
    OutboxProcessor processor,
    IOptions<OutboxOptions> options,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await processor.ProcessBatchAsync(stoppingToken);
            }
#pragma warning disable CA1031 // A failed poll, such as a database restart, must not stop the loop.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogPollFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox polling failed")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
