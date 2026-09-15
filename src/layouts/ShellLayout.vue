<script setup lang="ts">
import BrandLogo from '@/components/BrandLogo.vue'
import { IonIcon } from '@ionic/vue'
import { homeOutline, cartOutline, cubeOutline, restaurantOutline, pricetagsOutline, walletOutline, settingsOutline } from 'ionicons/icons'
import { nextTick, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import BottomNav from '@/components/BottomNav.vue'
import SyncPill from '@/components/SyncPill.vue'
import { useBusinessStore } from '@/stores/business'

const store = useBusinessStore()
const route = useRoute()
const scrollContainer = ref<HTMLElement | null>(null)
const mainContent = ref<HTMLElement | null>(null)
const items = [
  { label: 'Dashboard', path: '/dashboard', icon: homeOutline },
  { label: 'Sales', path: '/sales', icon: cartOutline },
  { label: 'Inventory', path: '/inventory', icon: cubeOutline },
  { label: 'Products & recipes', path: '/products', icon: pricetagsOutline },
  { label: 'Production', path: '/production', icon: restaurantOutline },
  { label: 'Expenses', path: '/expenses', icon: walletOutline },
  { label: 'Settings', path: '/settings', icon: settingsOutline }
]

function isActive(path: string): boolean {
  if (path === '/inventory') return route.path.startsWith('/inventory') || route.path.startsWith('/stock')
  return route.path.startsWith(path)
}

watch(
  () => route.fullPath,
  async () => {
    await nextTick()
    scrollContainer.value?.scrollTo({ top: 0, left: 0, behavior: 'auto' })
    mainContent.value?.focus({ preventScroll: true })
  }
)
</script>

<template>
  <div ref="scrollContainer" class="app-shell">
    <aside class="side-nav">
      <router-link class="side-brand" to="/dashboard">
        <BrandLogo reversed />
        <span class="product-label">Business Aid</span>
        <small>{{ store.business?.name }}</small>
      </router-link>
      <nav>
        <router-link v-for="item in items" :key="item.path" :to="item.path" :class="{ active: isActive(item.path) }" :aria-current="isActive(item.path) ? 'page' : undefined">
          <ion-icon :icon="item.icon" aria-hidden="true" />
          <span>{{ item.label }}</span>
        </router-link>
      </nav>
      <SyncPill />
    </aside>
    <main id="main-content" ref="mainContent" class="main-panel" tabindex="-1">
      <header class="mobile-header">
        <router-link class="mobile-brand" to="/dashboard"><BrandLogo reversed /><span>Business Aid</span></router-link>
        <SyncPill />
      </header>
      <router-view />
    </main>
    <BottomNav />
  </div>
</template>
