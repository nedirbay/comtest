/** Mirrors `Permissions` in the backend's Roles.cs. test/permissions.test.ts keeps the two equal. */
export const Permission = {
  TenantsManage: 'platform.tenants.manage',
  CatalogRead: 'catalog.read',
  CatalogWrite: 'catalog.write',
  InventoryRead: 'inventory.read',
  InventoryWrite: 'inventory.write',
  OrdersRead: 'orders.read',
  OrdersWrite: 'orders.write',
  OrdersManage: 'orders.manage',
  PaymentsConfirm: 'payments.confirm',
  PaymentsRefund: 'payments.refund',
  PaymentsRefundApprove: 'payments.refund.approve',
  CustomersRead: 'customers.read',
  PromotionsManage: 'promotions.manage',
  ReportsRead: 'reports.read',
  ReportsFinancial: 'reports.financial',
  SettingsManage: 'settings.manage',
  StaffManage: 'staff.manage',
  AuditRead: 'audit.read',
} as const

export const ASSIGNABLE_ROLES = ['Manager', 'Staff'] as const

const P = Permission

const MANAGER = [
  P.CatalogRead,
  P.CatalogWrite,
  P.InventoryRead,
  P.InventoryWrite,
  P.OrdersRead,
  P.OrdersWrite,
  P.OrdersManage,
  P.PaymentsConfirm,
  P.PaymentsRefundApprove,
  P.CustomersRead,
  P.PromotionsManage,
  P.ReportsRead,
] as const

const STAFF = [
  P.CatalogRead,
  P.InventoryRead,
  P.InventoryWrite,
  P.OrdersRead,
  P.OrdersWrite,
  P.CustomersRead,
] as const

const OWNER = [
  ...MANAGER,
  P.PaymentsRefund,
  P.ReportsFinancial,
  P.SettingsManage,
  P.StaffManage,
  P.AuditRead,
] as const

/**
 * The API has no endpoint for what each role may do, so the read-only matrix on the staff page uses
 * this copy of `Permissions.ByRole`. The server decides on its own and never reads this.
 */
export const ROLE_PERMISSIONS: Record<'StoreOwner' | 'Manager' | 'Staff', readonly string[]> = {
  StoreOwner: OWNER,
  Manager: MANAGER,
  Staff: STAFF,
}

/** The matrix's rows. Platform permissions belong to no store role. */
export const STORE_PERMISSIONS = [
  P.CatalogRead,
  P.CatalogWrite,
  P.InventoryRead,
  P.InventoryWrite,
  P.OrdersRead,
  P.OrdersWrite,
  P.OrdersManage,
  P.PaymentsConfirm,
  P.PaymentsRefund,
  P.PaymentsRefundApprove,
  P.CustomersRead,
  P.ReportsRead,
  P.ReportsFinancial,
  P.SettingsManage,
  P.StaffManage,
  P.AuditRead,
] as const
