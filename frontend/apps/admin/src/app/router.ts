import { safeReturnUrl, useAuthStore } from '@comtest/auth'
import { isAppLocale } from '@comtest/ui/locale'
import type { Pinia } from 'pinia'
import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { applyLocale, preferredLocale } from './i18n'
import { Permission as P } from './permissions'

declare module 'vue-router' {
  interface RouteMeta {
    /** Open without signing in. */
    public?: boolean
    /** For signed-out visitors only: a signed-in user is sent onward. */
    guestOnly?: boolean
    /** The frame around the page. See LayoutShell. */
    layout?: 'auth' | 'admin'
    /** Any one of these permissions lets the user in. */
    permission?: string | string[]
    /** An i18n key: the page's name in the breadcrumbs and the browser tab. */
    title?: string
    /** The route name one level up in the breadcrumbs. */
    parent?: string
  }
}

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: () => `/${preferredLocale()}`,
  },
  {
    path: '/:locale(tk|ru|en)',
    component: () => import('@/features/shell/LayoutShell.vue'),
    children: [
      {
        path: 'login',
        name: 'login',
        meta: { layout: 'auth', public: true, guestOnly: true },
        component: () => import('@/features/auth/pages/LoginPage.vue'),
      },
      {
        path: 'forgot-password',
        name: 'forgot-password',
        meta: { layout: 'auth', public: true, guestOnly: true },
        component: () => import('@/features/auth/pages/ForgotPasswordPage.vue'),
      },
      {
        path: 'two-factor-setup',
        name: 'two-factor-setup',
        meta: { layout: 'auth' },
        component: () => import('@/features/auth/pages/TwoFactorSetupPage.vue'),
      },
      {
        path: 'select-store',
        name: 'select-store',
        meta: { layout: 'auth' },
        component: () => import('@/features/auth/pages/SelectStorePage.vue'),
      },
      {
        path: '',
        name: 'dashboard',
        meta: { layout: 'admin', title: 'admin.nav.dashboard' },
        component: () => import('@/features/dashboard/pages/DashboardPage.vue'),
      },
      {
        path: 'security',
        name: 'security',
        meta: { layout: 'admin', title: 'admin.nav.security' },
        component: () => import('@/features/account/pages/SecurityPage.vue'),
      },
      {
        path: 'orders',
        name: 'orders',
        meta: { layout: 'admin', title: 'admin.nav.orders', permission: P.OrdersRead },
        component: () => import('@/features/orders/pages/OrdersPage.vue'),
      },
      {
        path: 'orders/:id',
        name: 'order-detail',
        meta: {
          layout: 'admin',
          title: 'admin.order.title',
          parent: 'orders',
          permission: P.OrdersRead,
        },
        component: () => import('@/features/orders/pages/OrderDetailPage.vue'),
      },
      {
        path: 'customers',
        name: 'customers',
        meta: { layout: 'admin', title: 'admin.nav.customers', permission: P.CustomersRead },
        component: () => import('@/features/customers/pages/CustomersPage.vue'),
      },
      {
        // Guests have no account, so their profile is found through one of their orders. A phone
        // number would otherwise have to sit in the address bar and the browser history.
        path: 'customers/guest/:orderId',
        name: 'customer-guest',
        meta: {
          layout: 'admin',
          title: 'admin.customers.profile',
          parent: 'customers',
          permission: P.CustomersRead,
        },
        component: () => import('@/features/customers/pages/CustomerProfilePage.vue'),
      },
      {
        path: 'customers/:userId',
        name: 'customer-profile',
        meta: {
          layout: 'admin',
          title: 'admin.customers.profile',
          parent: 'customers',
          permission: P.CustomersRead,
        },
        component: () => import('@/features/customers/pages/CustomerProfilePage.vue'),
      },
      {
        path: 'products',
        name: 'products',
        meta: { layout: 'admin', title: 'admin.nav.products', permission: P.CatalogRead },
        component: () => import('@/features/catalog/pages/ProductsPage.vue'),
      },
      {
        path: 'products/new',
        name: 'product-new',
        meta: {
          layout: 'admin',
          title: 'admin.products.new',
          parent: 'products',
          permission: P.CatalogWrite,
        },
        component: () => import('@/features/catalog/pages/ProductFormPage.vue'),
      },
      {
        path: 'products/:id',
        name: 'product-edit',
        meta: {
          layout: 'admin',
          title: 'admin.products.edit',
          parent: 'products',
          permission: P.CatalogRead,
        },
        component: () => import('@/features/catalog/pages/ProductFormPage.vue'),
      },
      {
        path: 'categories',
        name: 'categories',
        meta: { layout: 'admin', title: 'admin.nav.categories', permission: P.CatalogRead },
        component: () => import('@/features/catalog/pages/CategoriesPage.vue'),
      },
      {
        path: 'brands',
        name: 'brands',
        meta: { layout: 'admin', title: 'admin.nav.brands', permission: P.CatalogRead },
        component: () => import('@/features/catalog/pages/BrandsPage.vue'),
      },
      {
        path: 'attributes',
        name: 'attributes',
        meta: { layout: 'admin', title: 'admin.nav.attributes', permission: P.CatalogRead },
        component: () => import('@/features/catalog/pages/AttributesPage.vue'),
      },
      {
        path: 'stock',
        name: 'stock',
        meta: { layout: 'admin', title: 'admin.nav.stock', permission: P.InventoryRead },
        component: () => import('@/features/inventory/pages/StockPage.vue'),
      },
      {
        path: 'stock/movements',
        name: 'movements',
        meta: {
          layout: 'admin',
          title: 'admin.nav.movements',
          parent: 'stock',
          permission: P.InventoryRead,
        },
        component: () => import('@/features/inventory/pages/MovementsPage.vue'),
      },
      {
        path: 'warehouses',
        name: 'warehouses',
        meta: { layout: 'admin', title: 'admin.nav.warehouses', permission: P.InventoryRead },
        component: () => import('@/features/inventory/pages/WarehousesPage.vue'),
      },
      {
        path: 'settings',
        name: 'settings',
        meta: { layout: 'admin', title: 'admin.nav.settings', permission: P.SettingsManage },
        component: () => import('@/features/settings/pages/SettingsPage.vue'),
      },
      {
        path: 'staff',
        name: 'staff',
        meta: { layout: 'admin', title: 'admin.nav.staff', permission: P.StaffManage },
        component: () => import('@/features/staff/pages/StaffPage.vue'),
      },
      {
        path: 'platform/tenants',
        name: 'tenants',
        meta: { layout: 'admin', title: 'admin.nav.tenants', permission: P.TenantsManage },
        component: () => import('@/features/platform/pages/TenantsPage.vue'),
      },
      {
        path: 'forbidden',
        name: 'forbidden',
        meta: { layout: 'admin', title: 'pages.forbiddenTitle' },
        component: () => import('@/features/system/pages/ForbiddenPage.vue'),
      },
      {
        path: 'error',
        name: 'server-error',
        meta: { public: true },
        component: () => import('@/features/system/pages/ServerErrorPage.vue'),
      },
      {
        path: ':pathMatch(.*)*',
        name: 'not-found',
        meta: { public: true },
        component: () => import('@/features/system/pages/NotFoundPage.vue'),
      },
    ],
  },
  {
    // An address without a language gets one, and otherwise stays what it was.
    path: '/:pathMatch(.*)*',
    redirect: (to) => ({ path: `/${preferredLocale()}${to.path}`, query: to.query, hash: to.hash }),
  },
]

export function createAppRouter(pinia: Pinia) {
  const router = createRouter({ history: createWebHistory(), routes })

  /**
   * These checks only decide what to show. A user who bypasses them gets empty pages, because the
   * server authorizes every request by itself.
   */
  router.beforeEach(async (to) => {
    const locale = to.params.locale

    if (isAppLocale(locale)) {
      applyLocale(locale)
    }

    const auth = useAuthStore(pinia)

    await auth.bootstrap()

    if (to.meta.public) {
      if (to.meta.guestOnly && auth.isAuthenticated) {
        return safeReturnUrl(to.query.returnUrl, `/${String(locale)}`)
      }

      return true
    }

    if (!auth.isAuthenticated) {
      return { name: 'login', params: { locale }, query: { returnUrl: to.fullPath } }
    }

    if (auth.twoFactorSetupRequired) {
      return to.name === 'two-factor-setup'
        ? true
        : { name: 'two-factor-setup', params: { locale } }
    }

    if (to.name === 'two-factor-setup') {
      return { name: 'dashboard', params: { locale } }
    }

    if (!auth.hasActiveTenant && !auth.claims?.roles.includes('PlatformAdmin')) {
      return to.name === 'select-store' ? true : { name: 'select-store', params: { locale } }
    }

    if (to.meta.permission && !auth.can(to.meta.permission)) {
      return { name: 'forbidden', params: { locale } }
    }

    return true
  })

  // A chunk can disappear when a new version is deployed while this tab stays open.
  router.onError((error, to) => {
    if (/dynamically imported module|Importing a module script failed/i.test(String(error))) {
      window.location.assign(to.fullPath)
    } else {
      void router.replace({ name: 'server-error', params: { locale: preferredLocale() } })
    }
  })

  return router
}
