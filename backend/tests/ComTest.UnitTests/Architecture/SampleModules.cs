// Sample modules for testing the module boundary rule itself.
namespace ComTest.UnitTests.Architecture.Sample.Catalog
{
    internal sealed class CatalogSecret;
}

namespace ComTest.UnitTests.Architecture.Sample.Catalog.Contracts
{
    public sealed class ProductSummary;
}

namespace ComTest.UnitTests.Architecture.Sample.Orders
{
    /// <summary>Allowed: uses only the other module's contract.</summary>
    internal sealed class GoodOrderService
    {
        public Catalog.Contracts.ProductSummary? Product { get; set; }
    }

    /// <summary>Forbidden: reaches into the other module's internals.</summary>
    internal sealed class BadOrderService
    {
        public Catalog.CatalogSecret? Secret { get; set; }
    }
}
