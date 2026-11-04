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
        services.AddScoped<Modules.Catalog.Contracts.ICatalogReader, Modules.Catalog.CatalogReader>();
        services.AddScoped<Modules.Tenancy.Contracts.ITenantSettings, Modules.Tenancy.TenantSettingsReader>();
        services.AddScoped<Modules.Inventory.Contracts.IStockService, Modules.Inventory.StockService>();
        services.AddScoped<Modules.Media.Contracts.IMediaLibrary, Modules.Media.MediaHandlers>();
        services.AddScoped<Abstractions.IDomainEventSubscriber<Domain.Modules.Media.MediaUploaded>, Modules.Media.MediaProcessor>();
        services.AddScoped<Modules.Customers.Contracts.IAddressBook, Modules.Customers.AddressHandlers>();
        services.AddScoped<Modules.Shipping.Contracts.IShippingRates, Modules.Shipping.ShippingHandlers>();
        services.AddScoped<Modules.Payments.Contracts.IPaymentProvider, Modules.Payments.CashOnDeliveryProvider>();
        services.AddScoped<Modules.Payments.Contracts.IPaymentProvider, Modules.Payments.BankTransferProvider>();
        services.AddScoped<Modules.Payments.Contracts.IOrderPayments, Modules.Payments.OrderPayments>();
        services.AddScoped<Modules.Ordering.Contracts.IOrderPaymentSync, Modules.Ordering.OrderPaymentSync>();
        services.AddScoped<Modules.Ordering.Contracts.ISalesReader, Modules.Ordering.SalesReader>();
        services.AddScoped<Modules.Ordering.CartStore>();
        services.AddScoped<Modules.Ordering.CartPricing>();
        services.AddScoped<Modules.Ordering.OrderWorkflow>();

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
