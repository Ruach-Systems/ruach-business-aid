<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { IonIcon } from '@ionic/vue'
import { addOutline, removeOutline, checkmarkCircleOutline, cartOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { productCostCentavos, localDateKey } from '@/domain/calculations'
import { formatMoney } from '@/composables/formatters'

const store = useBusinessStore()
const quantities = reactive<Record<string, number>>({})
const deductInventory = ref(true)
const saleDate = ref(localDateKey())
const saving = ref(false)
const message = ref('')
const error = ref('')
const lines = computed(() => store.products.map((product) => {
  const quantity = Number(quantities[product.id] || 0)
  const unitCost = productCostCentavos(product, store.inventoryItems)
  return { product, quantity, unitCost, revenue: quantity * product.sellingPriceCentavos, cost: quantity * unitCost }
}).filter((line) => line.quantity > 0))
const totals = computed(() => ({
  items: lines.value.reduce((sum, line) => sum + line.quantity, 0), revenue: lines.value.reduce((sum, line) => sum + line.revenue, 0),
  cost: lines.value.reduce((sum, line) => sum + line.cost, 0), profit: lines.value.reduce((sum, line) => sum + line.revenue - line.cost, 0)
}))
function change(id: string, delta: number): void { quantities[id] = Math.max(0, Number(quantities[id] || 0) + delta) }

async function save(): Promise<void> {
  saving.value = true; error.value = ''; message.value = ''
  try {
    const sale = await store.recordSale(quantities, deductInventory.value, saleDate.value)
    Object.keys(quantities).forEach((key) => { quantities[key] = 0 })
    message.value = `${sale.totalItems} items recorded. Estimated profit ${formatMoney(sale.totalProfitCentavos)}.`
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'Unable to save sale.' }
  finally { saving.value = false }
}
</script>

<template>
  <div class="page narrow"><header class="page-heading"><div><span class="eyebrow">Fast daily entry</span><h1>What sold today?</h1><p>Enter totals by product—no checkout screen required.</p></div></header>
    <form class="stack" @submit.prevent="save">
      <section class="card form-card"><div class="field"><label for="sale-date">Sale date</label><input id="sale-date" v-model="saleDate" type="date" /></div></section>
      <section class="stack sale-lines">
        <article v-for="product in store.products" :key="product.id" class="card sale-line">
          <div class="list-main"><strong>{{ product.name }}</strong><small>{{ formatMoney(product.sellingPriceCentavos) }} · cost {{ formatMoney(productCostCentavos(product, store.inventoryItems)) }}</small></div>
          <div class="stepper"><button type="button" :aria-label="`Decrease ${product.name} quantity`" @click="change(product.id, -1)"><ion-icon :icon="removeOutline" aria-hidden="true" /></button><input v-model.number="quantities[product.id]" type="number" min="0" step="1" inputmode="numeric" :aria-label="`${product.name} quantity sold`" /><button type="button" :aria-label="`Increase ${product.name} quantity`" @click="change(product.id, 1)"><ion-icon :icon="addOutline" aria-hidden="true" /></button></div>
        </article>
      </section>
      <div v-if="!store.products.length" class="card empty"><span class="list-icon"><ion-icon :icon="cartOutline" /></span><h3>Add products first</h3><p>Products appear here for quick daily quantity entry.</p><router-link class="button primary small" to="/products/new">Add product</router-link></div>
      <section class="card dark totals-card" aria-label="Sale totals" aria-live="polite"><div><span class="card-label">Items</span><strong>{{ totals.items }}</strong></div><div><span class="card-label">Sales</span><strong>{{ formatMoney(totals.revenue) }}</strong></div><div><span class="card-label">Product cost</span><strong>{{ formatMoney(totals.cost) }}</strong></div><div class="profit"><span class="card-label">Estimated profit</span><strong>{{ formatMoney(totals.profit) }}</strong></div></section>
      <label class="toggle-row"><span><strong>Deduct inventory</strong><small>Uses each product’s recipe or finished stock mode.</small></span><input v-model="deductInventory" class="toggle" type="checkbox" /></label>
      <div v-if="message" class="success-banner" role="status"><ion-icon :icon="checkmarkCircleOutline" aria-hidden="true" /> {{ message }}</div><div v-if="error" class="error-banner" role="alert">{{ error }}</div>
      <button class="button primary block" :disabled="saving || totals.items === 0">{{ saving ? 'Saving sale…' : `Save ${totals.items || ''} item${totals.items === 1 ? '' : 's'}` }}</button>
    </form>
  </div>
</template>

<style scoped>
.sale-line { display: flex; align-items: center; gap: 14px; }.stepper { display: grid; grid-template-columns: 44px 48px 44px; align-items: center; gap: 4px; }.stepper button { width: 44px; height: 44px; border: 0; border-radius: 11px; display: grid; place-items: center; background: var(--mashal-purple-soft); color: var(--mashal-purple); }.stepper input { width: 48px; height: 44px; text-align: center; border: 1px solid var(--mashal-line); border-radius: 10px; background: var(--mashal-cloud); }.totals-card { display: grid; grid-template-columns: repeat(2,1fr); gap: 16px; }.totals-card > div { display: grid; gap: 6px; }.totals-card strong { font-size: 18px; }.totals-card .profit strong { color: var(--mashal-on-dark); }.success-banner { display: flex; align-items: center; gap: 7px; }
@media (max-width: 420px) { .sale-line { align-items: flex-start; }.stepper { grid-template-columns: 44px 48px 44px; } }
</style>
