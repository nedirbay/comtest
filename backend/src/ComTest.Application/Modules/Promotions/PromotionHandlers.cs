using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Application.Modules.Promotions.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Promotions;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Promotions;

public sealed record SavePromotionRequest(
    string? Code,
    string Name,
    PromotionType Type,
    decimal Value = 0m,
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? EndsAt = null,
    decimal? MinSubtotal = null,
    Guid? CategoryId = null,
    int? UsageLimit = null,
    int? PerCustomerLimit = null,
    bool CombinesWithOthers = false,
    bool IsActive = true);

internal sealed class SavePromotionRequestValidator : AbstractValidator<SavePromotionRequest>
{
    private const decimal MaxAmount = 9_999_999_999m;

    public SavePromotionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Code!)
            .Must(code => PromotionCode.Normalize(code).Length is >= PromotionCode.MinLength and <= PromotionCode.MaxLength)
            .Must(code => System.Text.RegularExpressions.Regex.IsMatch(PromotionCode.Normalize(code), PromotionCode.Pattern))
            .When(x => x.Code is not null);
        RuleFor(x => x.Value).InclusiveBetween(0.01m, 100m).When(x => x.Type == PromotionType.Percentage);
        RuleFor(x => x.Value).InclusiveBetween(0.01m, MaxAmount).When(x => x.Type == PromotionType.FixedAmount);
        RuleFor(x => x.MinSubtotal).InclusiveBetween(0m, MaxAmount).When(x => x.MinSubtotal is not null);
        RuleFor(x => x.UsageLimit).GreaterThan(0).When(x => x.UsageLimit is not null);
        RuleFor(x => x.PerCustomerLimit).GreaterThan(0).When(x => x.PerCustomerLimit is not null);
        RuleFor(x => x.EndsAt).Must((request, end) => request.StartsAt is null || end > request.StartsAt).When(x => x.EndsAt is not null);
    }
}

public sealed record ListPromotionsQuery(PageRequest Page, string? Search) : IQuery<PagedResult<PromotionDto>>;

public sealed record GetPromotionQuery(Guid Id) : IQuery<PromotionDto>;

public sealed record CreatePromotionCommand(SavePromotionRequest Request) : ICommand<PromotionDto>;

public sealed record UpdatePromotionCommand(Guid Id, SavePromotionRequest Request) : ICommand<PromotionDto>;

public sealed record DeletePromotionCommand(Guid Id) : ICommand<bool>;

public sealed class PromotionHandlers(
    IApplicationDbContext db,
    ITenantSettings tenantSettings,
    ICatalogReader catalog) :
    IQueryHandler<ListPromotionsQuery, PagedResult<PromotionDto>>,
    IQueryHandler<GetPromotionQuery, PromotionDto>,
    ICommandHandler<CreatePromotionCommand, PromotionDto>,
    ICommandHandler<UpdatePromotionCommand, PromotionDto>,
    ICommandHandler<DeletePromotionCommand, bool>
{
    private const string UniqueViolation = "23505";

    public async Task<Result<PagedResult<PromotionDto>>> HandleAsync(ListPromotionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var promotions = db.Promotions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var upper = term.ToUpperInvariant();

#pragma warning disable CA1304, CA1311, CA1862 // This ToUpper runs in the database as upper(), not in .NET.
            promotions = promotions.Where(item => item.Name.ToUpper().Contains(upper) || (item.Code != null && item.Code.Contains(upper)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var total = await promotions.LongCountAsync(cancellationToken);
        var items = await promotions
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PromotionDto>(items.Select(ToDto).ToList(), query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<Result<PromotionDto>> HandleAsync(GetPromotionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var promotion = await db.Promotions.AsNoTracking().FirstOrDefaultAsync(item => item.Id == query.Id, cancellationToken);

        return promotion is null ? Error.NotFound(ErrorCodes.Promotion.NotFound) : ToDto(promotion);
    }

    public async Task<Result<PromotionDto>> HandleAsync(CreatePromotionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await tenantSettings.GetCurrentAsync(cancellationToken) is not { } settings)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var problem = await CheckAsync(command.Request, null, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        var promotion = Promotion.Create(ToTerms(command.Request), settings.DefaultCurrency);

        db.Promotions.Add(promotion);

        return await SaveAsync(promotion, cancellationToken);
    }

    public async Task<Result<PromotionDto>> HandleAsync(UpdatePromotionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var promotion = await db.Promotions.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound(ErrorCodes.Promotion.NotFound);
        }

        var problem = await CheckAsync(command.Request, command.Id, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        promotion.Update(ToTerms(command.Request));

        return await SaveAsync(promotion, cancellationToken);
    }

    public async Task<Result<bool>> HandleAsync(DeletePromotionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var promotion = await db.Promotions.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound(ErrorCodes.Promotion.NotFound);
        }

        db.Promotions.Remove(promotion);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<Error?> CheckAsync(SavePromotionRequest request, Guid? ownId, CancellationToken cancellationToken)
    {
        if (request.CategoryId is { } categoryId && !await catalog.CategoryExistsAsync(categoryId, cancellationToken))
        {
            return Error.NotFound(ErrorCodes.Catalog.CategoryNotFound);
        }

        if (request.Code is null)
        {
            return null;
        }

        var code = PromotionCode.Normalize(request.Code);
        var taken = await db.Promotions.AnyAsync(item => item.Code == code && item.Id != ownId, cancellationToken);

        return taken ? Error.Conflict(ErrorCodes.Promotion.CodeTaken) : null;
    }

    private async Task<Result<PromotionDto>> SaveAsync(Promotion promotion, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is System.Data.Common.DbException { SqlState: UniqueViolation })
        {
            return Error.Conflict(ErrorCodes.Promotion.CodeTaken);
        }

        return ToDto(promotion);
    }

    private static PromotionTerms ToTerms(SavePromotionRequest request) => new(
        request.Code is null ? null : PromotionCode.Normalize(request.Code),
        request.Name.Trim(),
        request.Type,
        request.Value,
        request.StartsAt,
        request.EndsAt,
        request.MinSubtotal,
        request.CategoryId,
        request.UsageLimit,
        request.PerCustomerLimit,
        request.CombinesWithOthers,
        request.IsActive);

    private static PromotionDto ToDto(Promotion item) => new(
        item.Id,
        item.Code,
        item.Name,
        item.Type,
        item.Value,
        item.Currency,
        item.StartsAt,
        item.EndsAt,
        item.MinSubtotal,
        item.CategoryId,
        item.UsageLimit,
        item.UsageCount,
        item.PerCustomerLimit,
        item.CombinesWithOthers,
        item.IsActive);
}
