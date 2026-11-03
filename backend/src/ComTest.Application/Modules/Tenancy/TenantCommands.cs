using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Tenancy;
using ComTest.Shared.Domain;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Tenancy;

internal static class TenancyRules
{
    public const string SlugPattern = "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$";

    public static readonly string[] Languages = ["tk", "ru", "en"];

    public static readonly IReadOnlySet<string> ReservedSlugs = new HashSet<string>(StringComparer.Ordinal)
    {
        "www", "api", "app", "admin", "platform", "static", "cdn", "assets", "mail", "smtp", "ftp", "status",
        "support", "help", "docs", "blog", "auth", "login", "dashboard", "billing", "test", "staging", "dev",
    };

    public static IRuleBuilderOptions<T, string> ValidCurrency<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(Money.IsSupportedCurrency);

    public static IRuleBuilderOptions<T, string> ValidLanguage<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(language => Languages.Contains(language));
}

public sealed record CreateTenantCommand(Guid OwnerId, string Name, string Slug, string DefaultCurrency, string DefaultLanguage)
    : ICommand<TenantDto>;

public sealed record CreateTenantRequest(string Name, string Slug, string DefaultCurrency = "TMT", string DefaultLanguage = "tk");

internal sealed class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().Matches(TenancyRules.SlugPattern);
        RuleFor(x => x.DefaultCurrency).ValidCurrency();
        RuleFor(x => x.DefaultLanguage).ValidLanguage();
    }
}

public sealed class CreateTenantHandler(IApplicationDbContext db, ITenantScope tenantScope, TimeProvider timeProvider)
    : ICommandHandler<CreateTenantCommand, TenantDto>
{
    public async Task<Result<TenantDto>> HandleAsync(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TenancyRules.ReservedSlugs.Contains(command.Slug))
        {
            return Error.Conflict(ErrorCodes.Tenant.SlugReserved);
        }

        if (await db.Tenants.AnyAsync(tenant => tenant.Slug == command.Slug, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Tenant.SlugTaken);
        }

        var tenant = Tenant.Create(command.Name, command.Slug, command.DefaultCurrency, command.DefaultLanguage, timeProvider.GetUtcNow());

        // Row-level security only allows the owner row to be written for the current tenant.
        tenantScope.Use(tenant.Id);

        db.Tenants.Add(tenant);
        db.TenantMembers.Add(TenantMember.Create(tenant.Id, command.OwnerId, Roles.StoreOwner));

        await db.SaveChangesAsync(cancellationToken);

        return new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Status, tenant.Plan);
    }
}

public sealed record GetPublicStoreQuery(Guid TenantId) : IQuery<PublicStoreDto>;

public sealed class GetPublicStoreHandler(IApplicationDbContext db) : IQueryHandler<GetPublicStoreQuery, PublicStoreDto>
{
    public async Task<Result<PublicStoreDto>> HandleAsync(GetPublicStoreQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var store = await db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == query.TenantId)
            .Select(tenant => new PublicStoreDto(
                tenant.Name,
                tenant.Slug,
                tenant.DefaultCurrency,
                tenant.DefaultLanguage,
                tenant.LogoUrl,
                tenant.ContactEmail,
                tenant.ContactPhone,
                tenant.Address,
                tenant.WorkingHours))
            .FirstOrDefaultAsync(cancellationToken);

        return store is null ? Error.NotFound(ErrorCodes.Tenant.NotFound) : store;
    }
}

public sealed record GetStoreSettingsQuery(Guid TenantId) : IQuery<StoreSettingsDto>;

public sealed class GetStoreSettingsHandler(IApplicationDbContext db) : IQueryHandler<GetStoreSettingsQuery, StoreSettingsDto>
{
    public async Task<Result<StoreSettingsDto>> HandleAsync(GetStoreSettingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(item => item.Id == query.TenantId, cancellationToken);

        return tenant is null ? Error.NotFound(ErrorCodes.Tenant.NotFound) : tenant.ToSettingsDto();
    }
}

public sealed record UpdateStoreSettingsRequest(
    string Name,
    string DefaultCurrency,
    string DefaultLanguage,
    string? LogoUrl,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    string? WorkingHours,
    uint Version);

internal sealed class UpdateStoreSettingsRequestValidator : AbstractValidator<UpdateStoreSettingsRequest>
{
    public UpdateStoreSettingsRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultCurrency).ValidCurrency();
        RuleFor(x => x.DefaultLanguage).ValidLanguage();

        // Only http(s): a javascript: URL here would run in every visitor's browser.
        RuleFor(x => x.LogoUrl)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            .When(x => x.LogoUrl is not null);
        RuleFor(x => x.ContactEmail).MaximumLength(254).EmailAddress().When(x => x.ContactEmail is not null);
        RuleFor(x => x.ContactPhone).MaximumLength(32);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.WorkingHours).MaximumLength(200);
    }
}

public sealed record UpdateStoreSettingsCommand(Guid TenantId, UpdateStoreSettingsRequest Settings) : ICommand<StoreSettingsDto>;

public sealed class UpdateStoreSettingsHandler(IApplicationDbContext db, ITenantDirectory directory)
    : ICommandHandler<UpdateStoreSettingsCommand, StoreSettingsDto>
{
    public async Task<Result<StoreSettingsDto>> HandleAsync(UpdateStoreSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenant = await db.Tenants.FirstOrDefaultAsync(item => item.Id == command.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound(ErrorCodes.Tenant.NotFound);
        }

        var settings = command.Settings;

        if (tenant.Version != settings.Version)
        {
            return Error.Conflict(ErrorCodes.Common.ConcurrencyConflict);
        }

        tenant.UpdateSettings(
            settings.Name,
            settings.DefaultCurrency,
            settings.DefaultLanguage,
            settings.LogoUrl,
            settings.ContactEmail,
            settings.ContactPhone,
            settings.Address,
            settings.WorkingHours);

        await db.SaveChangesAsync(cancellationToken);
        directory.Invalidate(new TenantInfo(tenant.Id, tenant.Slug, tenant.Name, tenant.Status));

        return tenant.ToSettingsDto();
    }
}

public sealed record ListTenantsQuery(PageRequest Page, TenantStatus? Status) : IQuery<PagedResult<TenantDto>>;

public sealed class ListTenantsHandler(IApplicationDbContext db) : IQueryHandler<ListTenantsQuery, PagedResult<TenantDto>>
{
    public async Task<Result<PagedResult<TenantDto>>> HandleAsync(ListTenantsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenants = db.Tenants.AsNoTracking();

        if (query.Status is { } status)
        {
            tenants = tenants.Where(tenant => tenant.Status == status);
        }

        var total = await tenants.LongCountAsync(cancellationToken);

        var items = await tenants
            .OrderBy(tenant => tenant.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(tenant => new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Status, tenant.Plan))
            .ToListAsync(cancellationToken);

        return new PagedResult<TenantDto>(items, query.Page.Page, query.Page.PageSize, total);
    }
}

public sealed record SetTenantStatusCommand(Guid TenantId, TenantStatus Status) : ICommand<TenantDto>;

public sealed class SetTenantStatusHandler(IApplicationDbContext db, ITenantDirectory directory)
    : ICommandHandler<SetTenantStatusCommand, TenantDto>
{
    public async Task<Result<TenantDto>> HandleAsync(SetTenantStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenant = await db.Tenants.FirstOrDefaultAsync(item => item.Id == command.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound(ErrorCodes.Tenant.NotFound);
        }

        if (command.Status == TenantStatus.Suspended)
        {
            tenant.Suspend();
        }
        else
        {
            tenant.Activate();
        }

        await db.SaveChangesAsync(cancellationToken);
        directory.Invalidate(new TenantInfo(tenant.Id, tenant.Slug, tenant.Name, tenant.Status));

        return new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Status, tenant.Plan);
    }
}

public sealed class TenantSettingsReader(IApplicationDbContext db, ITenantContext tenantContext) : ITenantSettings
{
    public async Task<TenantSettingsSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return null;
        }

        return await db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new TenantSettingsSnapshot(
                tenant.Id,
                tenant.Name,
                tenant.DefaultCurrency,
                tenant.DefaultLanguage,
                tenant.MaxProducts,
                tenant.AcceptsCashOnDelivery,
                tenant.AcceptsBankTransfer,
                tenant.BankTransferInstructions,
                tenant.RefundApprovalThreshold,
                tenant.StorageQuotaMegabytes))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed record UpdatePaymentSettingsRequest(
    bool AcceptsCashOnDelivery,
    bool AcceptsBankTransfer,
    string? BankTransferInstructions,
    decimal? RefundApprovalThreshold);

internal sealed class UpdatePaymentSettingsRequestValidator : AbstractValidator<UpdatePaymentSettingsRequest>
{
    public UpdatePaymentSettingsRequestValidator()
    {
        RuleFor(x => x.BankTransferInstructions).MaximumLength(2000);
        RuleFor(x => x.BankTransferInstructions).NotEmpty().When(x => x.AcceptsBankTransfer);
        RuleFor(x => x.RefundApprovalThreshold).GreaterThan(0m).When(x => x.RefundApprovalThreshold is not null);
    }
}

public sealed record PaymentSettingsDto(
    bool AcceptsCashOnDelivery,
    bool AcceptsBankTransfer,
    string? BankTransferInstructions,
    decimal? RefundApprovalThreshold);

public sealed record GetPaymentSettingsQuery(Guid TenantId) : IQuery<PaymentSettingsDto>;

public sealed class GetPaymentSettingsHandler(IApplicationDbContext db) : IQueryHandler<GetPaymentSettingsQuery, PaymentSettingsDto>
{
    public async Task<Result<PaymentSettingsDto>> HandleAsync(GetPaymentSettingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = await db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == query.TenantId)
            .Select(tenant => new PaymentSettingsDto(
                tenant.AcceptsCashOnDelivery,
                tenant.AcceptsBankTransfer,
                tenant.BankTransferInstructions,
                tenant.RefundApprovalThreshold))
            .FirstOrDefaultAsync(cancellationToken);

        return settings is null ? Error.NotFound(ErrorCodes.Tenant.NotFound) : settings;
    }
}

public sealed record UpdatePaymentSettingsCommand(Guid TenantId, UpdatePaymentSettingsRequest Settings) : ICommand<bool>;

public sealed class UpdatePaymentSettingsHandler(IApplicationDbContext db) : ICommandHandler<UpdatePaymentSettingsCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(UpdatePaymentSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenant = await db.Tenants.FirstOrDefaultAsync(item => item.Id == command.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound(ErrorCodes.Tenant.NotFound);
        }

        var settings = command.Settings;

        tenant.UpdatePaymentSettings(
            settings.AcceptsCashOnDelivery,
            settings.AcceptsBankTransfer,
            settings.BankTransferInstructions,
            settings.RefundApprovalThreshold);

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}

internal static class TenantMappings
{
    public static StoreSettingsDto ToSettingsDto(this Tenant tenant) => new(
        tenant.Name,
        tenant.Slug,
        tenant.Plan,
        tenant.MaxProducts,
        tenant.MaxStaff,
        tenant.StorageQuotaMegabytes,
        tenant.DefaultCurrency,
        tenant.DefaultLanguage,
        tenant.LogoUrl,
        tenant.ContactEmail,
        tenant.ContactPhone,
        tenant.Address,
        tenant.WorkingHours,
        tenant.Version);
}
