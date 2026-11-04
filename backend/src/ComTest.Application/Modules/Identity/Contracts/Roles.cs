namespace ComTest.Application.Modules.Identity.Contracts;

public static class Roles
{
    public const string PlatformAdmin = nameof(PlatformAdmin);
    public const string StoreOwner = nameof(StoreOwner);
    public const string Manager = nameof(Manager);
    public const string Staff = nameof(Staff);
    public const string Customer = nameof(Customer);

    /// <summary>Roles with access to other people's data. They must use two-factor sign-in.</summary>
    public static readonly IReadOnlySet<string> RequireTwoFactor =
        new HashSet<string>(StringComparer.Ordinal) { PlatformAdmin, StoreOwner, Manager, Staff };
}

public static class Permissions
{
    public const string ClaimType = "permission";

    public const string TenantsManage = "platform.tenants.manage";
    public const string CatalogRead = "catalog.read";
    public const string CatalogWrite = "catalog.write";
    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";

    /// <summary>Cancelling and returning orders, which undo sales.</summary>
    public const string OrdersManage = "orders.manage";

    /// <summary>Recording that money has been received.</summary>
    public const string PaymentsConfirm = "payments.confirm";
    public const string PaymentsRefund = "payments.refund";

    /// <summary>Second approval of large refunds. Deliberately separate from requesting one.</summary>
    public const string PaymentsRefundApprove = "payments.refund.approve";
    public const string CustomersRead = "customers.read";
    public const string PromotionsManage = "promotions.manage";
    public const string ReportsRead = "reports.read";
    public const string ReportsFinancial = "reports.financial";
    public const string SettingsManage = "settings.manage";
    public const string StaffManage = "staff.manage";
    public const string AuditRead = "audit.read";

    private static readonly string[] ManagerPermissions =
    [
        CatalogRead, CatalogWrite, InventoryRead, InventoryWrite, OrdersRead, OrdersWrite, OrdersManage, PaymentsConfirm, PaymentsRefundApprove, CustomersRead, PromotionsManage, ReportsRead,
    ];

    private static readonly string[] StaffPermissions =
    [
        CatalogRead, InventoryRead, InventoryWrite, OrdersRead, OrdersWrite, CustomersRead,
    ];

    private static readonly string[] OwnerPermissions =
    [
        .. ManagerPermissions, PaymentsRefund, ReportsFinancial, SettingsManage, StaffManage, AuditRead,
    ];

    /// <summary>Default permissions for each role, seeded as role claims.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ByRole { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Roles.PlatformAdmin] = [TenantsManage, AuditRead],
            [Roles.StoreOwner] = OwnerPermissions,
            [Roles.Manager] = ManagerPermissions,
            [Roles.Staff] = StaffPermissions,
            [Roles.Customer] = [],
        };
}
