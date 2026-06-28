import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'

const router = createRouter({
  history: createWebHistory(),
  scrollBehavior: () => ({ top: 0 }),
  routes: [
    {
      path: '/',
      component: () => import('@/layouts/StorefrontLayout.vue'),
      children: [
        { path: '', name: 'home', component: () => import('@/pages/HomePage.vue') },
        { path: 'products', name: 'products', component: () => import('@/pages/ProductListPage.vue') },
        { path: 'products/:id', name: 'product-detail', component: () => import('@/pages/ProductDetailPage.vue') },
        { path: 'cart', name: 'cart', component: () => import('@/pages/CartPage.vue') },
        {
          path: 'checkout',
          name: 'checkout',
          component: () => import('@/pages/CheckoutPage.vue'),
          meta: { requiresAuth: true },
        },
        {
          path: 'order-confirmation/:id',
          name: 'order-confirmation',
          component: () => import('@/pages/OrderConfirmationPage.vue'),
          meta: { requiresAuth: true },
        },
        {
          path: 'account',
          meta: { requiresAuth: true },
          children: [
            { path: 'orders', name: 'my-orders', component: () => import('@/pages/account/OrderHistoryPage.vue') },
            { path: 'profile', name: 'profile', component: () => import('@/pages/account/ProfilePage.vue') },
          ],
        },
      ],
    },
    {
      path: '/auth',
      children: [
        { path: 'login', name: 'login', component: () => import('@/pages/auth/LoginPage.vue') },
        { path: 'register', name: 'register', component: () => import('@/pages/auth/RegisterPage.vue') },
      ],
    },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
})

export default router
