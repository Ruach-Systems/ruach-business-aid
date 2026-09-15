import { createRouter, createWebHistory } from '@ionic/vue-router'
import SignInPage from '@/pages/SignInPage.vue'
import OnboardingPage from '@/pages/OnboardingPage.vue'
import ShellLayout from '@/layouts/ShellLayout.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/sign-in', component: SignInPage },
    { path: '/onboarding', component: OnboardingPage },
    {
      path: '/',
      component: ShellLayout,
      children: [
        { path: '', redirect: '/dashboard' },
        { path: 'dashboard', component: () => import('@/pages/DashboardPage.vue') },
        { path: 'sales', component: () => import('@/pages/SalesPage.vue') },
        { path: 'expenses', component: () => import('@/pages/ExpensesPage.vue') },
        { path: 'inventory', component: () => import('@/pages/InventoryPage.vue') },
        { path: 'inventory/new', component: () => import('@/pages/InventoryEditorPage.vue') },
        { path: 'inventory/:id', component: () => import('@/pages/InventoryDetailPage.vue') },
        { path: 'stock/add', component: () => import('@/pages/StockReceiptPage.vue') },
        { path: 'stock/adjust', component: () => import('@/pages/StockAdjustmentPage.vue') },
        { path: 'products', component: () => import('@/pages/ProductsPage.vue') },
        { path: 'products/new', component: () => import('@/pages/ProductEditorPage.vue') },
        { path: 'products/:id', component: () => import('@/pages/ProductEditorPage.vue') },
        { path: 'production', component: () => import('@/pages/ProductionPage.vue') },
        { path: 'production/new', component: () => import('@/pages/BatchEditorPage.vue') },
        { path: 'production/:id', component: () => import('@/pages/BatchEditorPage.vue') },
        { path: 'production/:id/complete', component: () => import('@/pages/BatchCompletePage.vue') },
        { path: 'more', component: () => import('@/pages/MorePage.vue') },
        { path: 'settings', component: () => import('@/pages/SettingsPage.vue') }
      ]
    },
    { path: '/:pathMatch(.*)*', redirect: '/dashboard' }
  ],
  scrollBehavior: () => ({ top: 0 })
})

const pageTitles: Array<[RegExp, string]> = [
  [/^\/dashboard$/, 'Dashboard'],
  [/^\/sales$/, 'Daily sales'],
  [/^\/expenses$/, 'Expenses'],
  [/^\/inventory\/new$/, 'New inventory item'],
  [/^\/inventory\/[^/]+$/, 'Inventory item'],
  [/^\/inventory$/, 'Inventory'],
  [/^\/stock\/add$/, 'Add stock'],
  [/^\/stock\/adjust$/, 'Adjust stock'],
  [/^\/products\/new$/, 'New product'],
  [/^\/products\/[^/]+$/, 'Edit product'],
  [/^\/products$/, 'Products'],
  [/^\/production\/new$/, 'Make batch'],
  [/^\/production\/[^/]+\/complete$/, 'Complete batch'],
  [/^\/production\/[^/]+$/, 'Batch draft'],
  [/^\/production$/, 'Production'],
  [/^\/settings$/, 'Settings'],
  [/^\/more$/, 'More'],
  [/^\/onboarding$/, 'Create business'],
  [/^\/sign-in$/, 'Sign in']
]

router.afterEach((to) => {
  const title = pageTitles.find(([pattern]) => pattern.test(to.path))?.[1] || 'Business Aid'
  document.title = `${title} | Mashal Business Aid`
})

export default router
