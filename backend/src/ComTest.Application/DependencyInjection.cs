using System.Reflection;
using ComTest.Shared.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(AssemblyMarker).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddMessageHandlers(assembly);
        services.AddScoped<Modules.Catalog.CatalogCache>();

        return services;
    }

    /// <summary>Registers every command and query handler in the assembly as scoped.</summary>
    public static IServiceCollection AddMessageHandlers(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var registrations =
            from type in assembly.GetTypes()
            where type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            from contract in type.GetInterfaces()
            where contract.IsGenericType
                && (contract.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                    || contract.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))
            select (contract, type);

        foreach (var (contract, type) in registrations)
        {
            services.AddScoped(contract, type);
        }

        return services;
    }
}
