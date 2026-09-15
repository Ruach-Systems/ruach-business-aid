<script setup lang="ts">
import { computed } from 'vue'
import { IonIcon } from '@ionic/vue'
import { addOutline, restaurantOutline, chevronForwardOutline, cubeOutline, checkmarkCircleOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { formatMoney, formatQuantity } from '@/composables/formatters'

const store = useBusinessStore()
const preparedProducts = computed(() => store.products.filter((product) => product.inventoryMode === 'prepared'))
const drafts = computed(() => store.batches.filter((batch) => batch.status === 'draft').sort((a,b) => b.updatedAt.localeCompare(a.updatedAt)))
const completed = computed(() => store.batches.filter((batch) => batch.status === 'completed').sort((a,b) => b.updatedAt.localeCompare(a.updatedAt)).slice(0, 6))
</script>

<template>
  <div class="page"><header class="page-heading"><div><span class="eyebrow">Prepared-in-advance products</span><h1>Production</h1><p>Plan a batch, review actual use, then post it to inventory.</p></div><router-link class="button primary small" to="/production/new"><ion-icon :icon="addOutline" /> Make batch</router-link></header>
    <div v-if="!preparedProducts.length" class="card soft setup-callout"><span class="list-icon"><ion-icon :icon="restaurantOutline" /></span><div><strong>Create a prepared product first</strong><p>Set a product to “Prepared in advance” and add its recipe. MASHAL will create finished stock from completed batches.</p></div><router-link class="button dark small" to="/products/new">Add product</router-link></div>

    <div class="split section">
      <section><div class="section-heading"><h2>Draft batches</h2><span class="pill">{{ drafts.length }} draft{{ drafts.length === 1 ? '' : 's' }}</span></div><div v-if="drafts.length" class="stack"><router-link v-for="batch in drafts" :key="batch.id" class="card batch-card" :to="`/production/${batch.id}`"><div class="batch-icon"><ion-icon :icon="restaurantOutline" /></div><div class="list-main"><span class="pill">Draft</span><h3>{{ batch.productName }}</h3><small>Planned {{ formatQuantity(batch.plannedYield) }} items · {{ batch.ingredients.length }} ingredients</small></div><ion-icon class="chevron" :icon="chevronForwardOutline" /></router-link></div><div v-else class="card empty"><span class="list-icon"><ion-icon :icon="restaurantOutline" /></span><h3>No draft batches</h3><p>Start a production plan. Inventory remains untouched until completion.</p><router-link v-if="preparedProducts.length" class="button primary small" to="/production/new">Make a batch</router-link></div></section>
      <section><div class="section-heading"><h2>Finished stock</h2></div><div v-if="preparedProducts.length" class="card stack"><div v-for="product in preparedProducts" :key="product.id" class="list-card compact"><span class="list-icon"><ion-icon :icon="cubeOutline" /></span><div class="list-main"><strong>{{ product.name }}</strong><small>Avg. {{ formatMoney(store.inventoryItems.find(item => item.id === product.finishedInventoryItemId)?.averageCostCentavos || 0) }}/item</small></div><div class="stock-count"><strong>{{ formatQuantity(store.inventoryItems.find(item => item.id === product.finishedInventoryItemId)?.currentQuantity || 0) }}</strong><small>ready</small></div></div></div><div v-else class="card empty compact"><p>No finished-stock products.</p></div></section>
    </div>

    <section v-if="completed.length" class="section"><div class="section-heading"><h2>Recently completed</h2></div><div class="completed-grid"><article v-for="batch in completed" :key="batch.id" class="card completed-card"><ion-icon :icon="checkmarkCircleOutline" /><div><strong>{{ batch.productName }}</strong><small>{{ batch.actualYield }} items · {{ formatMoney(batch.totalCostCentavos || 0) }} total</small></div><span>{{ new Date(batch.completedAt || batch.updatedAt).toLocaleDateString('en-PH') }}</span></article></div></section>
  </div>
</template>

<style scoped>
.setup-callout { display: flex; align-items: center; gap: 14px; }.setup-callout > div:nth-child(2) { flex: 1; }.setup-callout p { margin: 5px 0 0; color: var(--mashal-muted); font-size: 12px; line-height: 1.5; }.batch-card { display: flex; align-items: center; gap: 14px; }.batch-icon { width: 56px; height: 56px; display: grid; place-items: center; border-radius: 17px; background: var(--mashal-purple-soft); color: var(--mashal-purple); font-size: 25px; }.batch-card h3 { font-family: var(--ion-font-family); color: var(--mashal-navy); font-size: 20px; margin: 6px 0 3px; }.list-card.compact { padding: 0; }.stock-count { display: grid; text-align: right; }.stock-count small { color: var(--mashal-muted); font-size: 9px; }.completed-grid { display: grid; gap: 10px; }.completed-card { display: flex; align-items: center; gap: 11px; }.completed-card > ion-icon { color: var(--mashal-success); font-size: 23px; }.completed-card div { flex: 1; display: grid; gap: 4px; }.completed-card small, .completed-card > span { color: var(--mashal-muted); font-size: 10px; }.empty.compact { padding: 18px; }
@media (max-width: 560px) { .setup-callout { align-items: flex-start; flex-wrap: wrap; }.setup-callout .button { width: 100%; } }
@media (min-width: 700px) { .completed-grid { grid-template-columns: repeat(2,1fr); } }
</style>
