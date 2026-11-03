import {
  Box,
  Collection,
  Connection,
  Goods,
  House,
  Lock,
  OfficeBuilding,
  PriceTag,
  School,
  Setting,
  Sort,
  Tickets,
  User,
  UserFilled,
} from '@element-plus/icons-vue'
import type { Component } from 'vue'
import { Permission as P } from './permissions'

export interface NavItem {
  name: string
  labelKey: string
  icon: Component
  permission?: string
}

export interface NavSection {
  labelKey: string
  items: NavItem[]
}

/** The one list of what the panel offers. The side menu and the search palette both read it. */
export const NAV_SECTIONS: NavSection[] = [
  {
    labelKey: 'admin.nav.sectionMain',
    items: [{ name: 'dashboard', labelKey: 'admin.nav.dashboard', icon: House }],
  },
  {
    labelKey: 'admin.nav.sectionSales',
    items: [
      { name: 'orders', labelKey: 'admin.nav.orders', icon: Tickets, permission: P.OrdersRead },
      {
        name: 'customers',
        labelKey: 'admin.nav.customers',
        icon: User,
        permission: P.CustomersRead,
      },
    ],
  },
  {
    labelKey: 'admin.nav.sectionCatalog',
    items: [
      { name: 'products', labelKey: 'admin.nav.products', icon: Goods, permission: P.CatalogRead },
      {
        name: 'categories',
        labelKey: 'admin.nav.categories',
        icon: Connection,
        permission: P.CatalogRead,
      },
      { name: 'brands', labelKey: 'admin.nav.brands', icon: PriceTag, permission: P.CatalogRead },
      {
        name: 'attributes',
        labelKey: 'admin.nav.attributes',
        icon: Collection,
        permission: P.CatalogRead,
      },
    ],
  },
  {
    labelKey: 'admin.nav.sectionInventory',
    items: [
      { name: 'stock', labelKey: 'admin.nav.stock', icon: Box, permission: P.InventoryRead },
      {
        name: 'movements',
        labelKey: 'admin.nav.movements',
        icon: Sort,
        permission: P.InventoryRead,
      },
      {
        name: 'warehouses',
        labelKey: 'admin.nav.warehouses',
        icon: School,
        permission: P.InventoryRead,
      },
    ],
  },
  {
    labelKey: 'admin.nav.sectionStore',
    items: [
      {
        name: 'settings',
        labelKey: 'admin.nav.settings',
        icon: Setting,
        permission: P.SettingsManage,
      },
      { name: 'staff', labelKey: 'admin.nav.staff', icon: UserFilled, permission: P.StaffManage },
    ],
  },
  {
    labelKey: 'admin.nav.sectionPlatform',
    items: [
      {
        name: 'tenants',
        labelKey: 'admin.nav.tenants',
        icon: OfficeBuilding,
        permission: P.TenantsManage,
      },
    ],
  },
  {
    labelKey: 'admin.nav.sectionAccount',
    items: [{ name: 'security', labelKey: 'admin.nav.security', icon: Lock }],
  },
]

/** Keeps what the user may open, and drops a section left with nothing in it. */
export function visibleSections(can: (permission: string) => boolean): NavSection[] {
  return NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter((item) => !item.permission || can(item.permission)),
  })).filter((section) => section.items.length > 0)
}
