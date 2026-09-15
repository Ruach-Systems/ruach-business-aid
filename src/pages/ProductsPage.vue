<script setup lang="ts">
import { IonIcon } from '@ionic/vue'
import { addOutline, chevronForwardOutline, pricetagOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { formatMoney } from '@/composables/formatters'
import { productCostCentavos, grossMarginPercent } from '@/domain/calculations'

const store = useBusinessStore()
const modeLabel = (mode: string) => ({ prepared: 'Prepared in advance', untracked: 'Not tracked' }[mode] || mode)
</script>

<template>
  <div class="page">
    <header class="page-heading">
      <div><span class="eyebrow">Catalog & costing</span><h1>Products</h1><p>Set prices, recipes, and how every product affects stock.</p></div>
      <router-link class="button primary small" to="/products/new"><ion-icon :icon="addOutline" /> Add product</router-link>
    </header>
    <div v-if="store.products.length" class="product-grid">
      <router-link v-for="product in store.products" :key="product.id" class="card product-card" :to="`/products/${product.id}`">
        <div class="product-top"><span class="list-icon"><ion-icon :icon="pricetagOutline" /></span><span class="pill" :class="{ warning: product.inventoryMode === 'prepared', muted: product.inventoryMode === 'untracked' }">{{ modeLabel(product.inventoryMode) }}</span></div>
        <h2>{{ product.name }}</h2>
        <div class="price-row"><div><span>Price</span><strong>{{ formatMoney(product.sellingPriceCentavos) }}</strong></div><div><span>Cost</span><strong>{{ formatMoney(productCostCentavos(product, store.inventoryItems)) }}</strong></div></div>
        <footer><span>{{ grossMarginPercent(product.sellingPriceCentavos, productCostCentavos(product, store.inventoryItems)).toFixed(1) }}% margin</span><ion-icon :icon="chevronForwardOutline" /></footer>
      </router-link>
    </div>
    <div v-else class="card empty"><span class="list-icon"><ion-icon :icon="pricetagOutline" /></span><h3>No products yet</h3><p>Add what you sell, then optionally build its recipe from inventory items.</p><router-link class="button primary small" to="/products/new">Add first product</router-link></div>
  </div>
</template>

<style scoped>
.page-heading .button { flex: none; }.product-grid { display: grid; gap: 13px; }.product-card { display: grid; gap: 17px; transition: transform .15s, box-shadow .15s; }.product-card:hover { transform: translateY(-2px); box-shadow: var(--mashal-shadow); }.product-top, .price-row, footer { display: flex; align-items: center; justify-content: space-between; gap: 12px; }.product-card h2 { font-family: var(--ion-font-family); font-size: 24px; color: var(--mashal-navy); }.price-row { padding: 14px; background: var(--mashal-cloud); border-radius: 14px; }.price-row div { display: grid; gap: 4px; }.price-row div:last-child { text-align: right; }.price-row span, footer { color: var(--mashal-muted); font-size: 10px; text-transform: uppercase; letter-spacing: .08em; font-weight: 700; }.price-row strong { color: var(--mashal-ink); font-size: 15px; }.product-card footer { color: var(--mashal-purple); }.product-card footer ion-icon { font-size: 18px; }
@media (min-width: 700px) { .product-grid { grid-template-columns: repeat(2, minmax(0,1fr)); } }
@media (min-width: 1100px) { .product-grid { grid-template-columns: repeat(3, minmax(0,1fr)); } }
</style>
