<script setup lang="ts">
import { IonIcon } from '@ionic/vue'
import { chevronForwardOutline, pricetagsOutline, restaurantOutline, walletOutline, cubeOutline, settingsOutline } from 'ionicons/icons'

const links = [
  { label: 'Products & recipes', note: 'Pricing, costing, and recipes', path: '/products', icon: pricetagsOutline },
  { label: 'Production', note: 'Make and complete batches', path: '/production', icon: restaurantOutline },
  { label: 'Expenses', note: 'Daily operating costs', path: '/expenses', icon: walletOutline },
  { label: 'Stock actions', note: 'Receive or correct stock', path: '/stock/add', icon: cubeOutline },
  { label: 'Settings', note: 'Business, account, and cloud sync', path: '/settings', icon: settingsOutline }
]
</script>

<template>
  <div class="page narrow">
    <header class="page-heading"><div><span class="eyebrow">MASHAL</span><h1>More</h1><p>Manage the parts of your business you use less often.</p></div></header>
    <div class="stack">
      <router-link v-for="link in links" :key="link.path" class="card list-card" :to="link.path">
        <span class="list-icon"><ion-icon :icon="link.icon" /></span>
        <span class="list-main"><strong>{{ link.label }}</strong><small>{{ link.note }}</small></span>
        <ion-icon class="chevron" :icon="chevronForwardOutline" />
      </router-link>
    </div>
  </div>
</template>
