<script setup lang="ts">
import { IonIcon } from '@ionic/vue'
import { homeOutline, cartOutline, cubeOutline, gridOutline } from 'ionicons/icons'
import { useRoute } from 'vue-router'

const route = useRoute()
const items = [
  { label: 'Home', path: '/dashboard', icon: homeOutline },
  { label: 'Sales', path: '/sales', icon: cartOutline },
  { label: 'Inventory', path: '/inventory', icon: cubeOutline },
  { label: 'More', path: '/more', icon: gridOutline }
]

function isActive(path: string): boolean {
  if (path === '/inventory') return route.path.startsWith('/inventory') || route.path.startsWith('/stock')
  if (path === '/more') return ['/more', '/products', '/production', '/expenses', '/settings'].some((section) => route.path.startsWith(section))
  return route.path.startsWith(path)
}
</script>

<template>
  <nav class="bottom-nav" aria-label="Primary navigation">
    <router-link v-for="item in items" :key="item.path" :to="item.path" :class="{ active: isActive(item.path) }" :aria-current="isActive(item.path) ? 'page' : undefined">
      <ion-icon :icon="item.icon" aria-hidden="true" />
      <span>{{ item.label }}</span>
    </router-link>
  </nav>
</template>
