using ComTest.Api.ErrorHandling;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ComTest.Api.Security;

/// <summary>Validates every action argument that has a validator before the action runs.</summary>
public sealed class FluentValidationFilter(IServiceProvider services, ErrorResultFactory errors) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var failures = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var argument in context.ActionArguments.Values.Where(value => value is not null))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument!.GetType());

            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
            {
                if (!failures.TryGetValue(failure.PropertyName, out var messages))
                {
                    messages = [];
                    failures[failure.PropertyName] = messages;
                }

                messages.Add(failure.ErrorMessage);
            }
        }

        if (failures.Count > 0)
        {
            var details = failures.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
            context.Result = errors.Create(context.HttpContext, Error.Validation(ErrorCodes.Common.Validation, details));

            return;
        }

        await next();
    }
}
