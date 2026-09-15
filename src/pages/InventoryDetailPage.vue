<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { IonIcon } from '@ionic/vue'
import { addOutline, optionsOutline, cubeOutline, arrowDownOutline, arrowUpOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { formatMoney, formatQuantity } from '@/composables/formatters'
import PageBackLink from '@/components/PageBackLink.vue'

const store = useBusinessStore()
const route = useRoute()
const item = computed(() => store.inventoryItems.find((candidate) => candidate.id === route.params.id))
const history = computed(() => [...store.movements].filter((movement) => movement.inventoryItemId === item.value?.id).sort((a,b) => b.createdAt.localeCompare(a.createdAt)).slice(0, 20))
</script>

<template>
  <div v-if="item" class="page narrow">
    <PageBackLink to="/inventory" text="Back to inventory" />
    <header class="page-heading"><div><span class="eyebrow">Inventory item</span><h1>{{ item.name }}</h1><p>Current balance and movement history.</p></div></header>
    <section class="card dark stock-hero"><span class="card-label">Current stock</span><div class="stock-quantity">{{ formatQuantity(item.currentQuantity) }} <small>{{ item.baseUnit }}</small></div><div class="stock-meta"><span>Average cost {{ formatMoney(item.averageCostCentavos) }}/{{ item.baseUnit }}</span><span>Minimum {{ formatQuantity(item.minimumQuantity) }} {{ item.baseUnit }}</span></div></section>
    <div class="detail-actions"><router-link class="button primary" :to="`/stock/add?item=${item.id}`"><ion-icon :icon="addOutline" /> Add stock</router-link><router-link class="button ghost" :to="`/stock/adjust?item=${item.id}`"><ion-icon :icon="optionsOutline" /> Adjust</router-link></div>
    <section class="section"><div class="section-heading"><h2>Recent movements</h2></div><div v-if="history.length" class="stack"><article v-for="movement in history" :key="movement.id" class="card list-card"><span class="list-icon" :class="{ outgoing: movement.quantityDelta < 0 }"><ion-icon :icon="movement.quantityDelta >= 0 ? arrowDownOutline : arrowUpOutline" /></span><span class="list-main"><strong>{{ movement.movementType.replaceAll('_', ' ') }}</strong><small>{{ movement.note || movement.referenceType }} · {{ new Date(movement.createdAt).toLocaleDateString('en-PH') }}</small></span><span class="movement-qty" :class="{ negative: movement.quantityDelta < 0 }">{{ movement.quantityDelta > 0 ? '+' : '' }}{{ formatQuantity(movement.quantityDelta) }} {{ item.baseUnit }}</span></article></div><div v-else class="card empty"><span class="list-icon"><ion-icon :icon="cubeOutline" /></span><h3>No movements yet</h3><p>Add stock to establish the opening quantity and average cost.</p></div></section>
  </div>
  <div v-else class="page narrow"><div class="card empty"><h3>Inventory item not found</h3><router-link class="button ghost small" to="/inventory">Back to inventory</router-link></div></div>
</template>

<style scoped>
.stock-hero { display: grid; gap: 12px; }.stock-quantity { font-family: var(--ion-font-family); font-size: 48px; }.stock-quantity small { font: 600 14px Inter, sans-serif; color: #CBD5E1; }.stock-meta { display: flex; flex-wrap: wrap; gap: 8px 18px; color: #CBD5E1; font-size: 11px; }.detail-actions { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-top: 12px; }.list-icon.outgoing { background: var(--mashal-warning-soft); color: var(--mashal-warning-ink); }.movement-qty { color: var(--mashal-success-ink); font-size: 12px; font-weight: 800; white-space: nowrap; }.movement-qty.negative { color: var(--mashal-warning-ink); }
</style>
