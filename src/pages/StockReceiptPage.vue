<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useBusinessStore } from '@/stores/business'
import { unitDefinitions } from '@/types/models'
import { formatMoney, formatQuantity } from '@/composables/formatters'
import { pesosToCentavos, weightedAverageCost } from '@/domain/calculations'
import PageBackLink from '@/components/PageBackLink.vue'

const store = useBusinessStore()
const route = useRoute()
const router = useRouter()
const sourceItemId = String(route.query.item || '')
const initialItemId = sourceItemId || store.inventoryItems[0]?.id || ''
const inventoryItemId = ref(initialItemId)
const quantity = ref(1)
const unit = ref(store.inventoryItems.find((item) => item.id === initialItemId)?.baseUnit || 'pc')
const totalCost = ref(0)
const note = ref('')
const saving = ref(false)
const error = ref('')
const selectedItem = computed(() => store.inventoryItems.find((item) => item.id === inventoryItemId.value))
const returnPath = computed(() => sourceItemId ? `/inventory/${sourceItemId}` : '/inventory')
const returnLabel = computed(() => sourceItemId ? 'Back to inventory item' : 'Back to inventory')
const compatibleUnits = computed(() => unitDefinitions.filter((definition) => {
  const item = selectedItem.value
  if (!item) return false
  if (item.unitKind === 'mass' || item.unitKind === 'volume') return definition.kind === item.unitKind
  return definition.code === item.baseUnit
}))
const selectedUnit = computed(() => compatibleUnits.value.find((definition) => definition.code === unit.value) || compatibleUnits.value[0])
const baseQuantity = computed(() => quantity.value * (selectedUnit.value?.toBase || 1))
const receiptUnitCost = computed(() => baseQuantity.value > 0 ? Math.round(pesosToCentavos(totalCost.value) / baseQuantity.value) : 0)
const resultingAverage = computed(() => selectedItem.value ? weightedAverageCost(selectedItem.value.currentQuantity, selectedItem.value.averageCostCentavos, baseQuantity.value, pesosToCentavos(totalCost.value)) : 0)

function itemChanged(): void {
  unit.value = compatibleUnits.value.find((definition) => definition.code === selectedItem.value?.baseUnit)?.code || compatibleUnits.value[0]?.code || 'pc'
}

async function save(): Promise<void> {
  if (!selectedItem.value || quantity.value <= 0 || totalCost.value <= 0) { error.value = 'Choose an item and enter a quantity and purchase cost greater than zero.'; return }
  saving.value = true
  try {
    await store.addStock({ inventoryItemId: selectedItem.value.id, quantity: quantity.value, unit: selectedUnit.value?.code || selectedItem.value.baseUnit, toBase: selectedUnit.value?.toBase || 1, totalCostCentavos: pesosToCentavos(totalCost.value), note: note.value })
    await router.replace(`/inventory/${selectedItem.value.id}`)
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'Unable to add stock.' }
  finally { saving.value = false }
}
</script>

<template>
  <div class="page narrow"><PageBackLink :to="returnPath" :text="returnLabel" /><header class="page-heading"><div><span class="eyebrow">Receive a purchase</span><h1>Add stock</h1><p>The purchase updates quantity and weighted-average cost.</p></div></header>
    <form class="stack" @submit.prevent="save">
      <section class="card form-card">
        <div class="field"><label for="receipt-item">Inventory item</label><select id="receipt-item" v-model="inventoryItemId" required @change="itemChanged"><option disabled value="">Choose item</option><option v-for="item in store.inventoryItems" :key="item.id" :value="item.id">{{ item.name }}</option></select></div>
        <div class="field"><label for="receipt-quantity">Quantity received</label><div class="field-row"><input id="receipt-quantity" v-model.number="quantity" type="number" min="0.0001" step="0.01" inputmode="decimal" required /><select v-model="unit" aria-label="Purchase unit"><option v-for="definition in compatibleUnits" :key="definition.code" :value="definition.code">{{ definition.code }}</option></select></div><span v-if="selectedItem && selectedUnit?.code !== selectedItem.baseUnit" class="field-help" aria-live="polite">Converts to {{ formatQuantity(baseQuantity) }} {{ selectedItem.baseUnit }}.</span></div>
        <div class="field"><label for="receipt-cost">Total purchase cost</label><div class="money-input"><span aria-hidden="true">₱</span><input id="receipt-cost" v-model.number="totalCost" type="number" min="0.01" step="0.01" inputmode="decimal" required /></div></div>
        <div class="field"><label for="receipt-note">Note <i>optional</i></label><textarea id="receipt-note" v-model="note" placeholder="Supplier, receipt, or other detail"></textarea></div>
      </section>
      <section v-if="selectedItem && totalCost > 0" class="card soft preview"><span class="card-label">Cost preview</span><div><span>Receipt unit cost</span><strong>{{ formatMoney(receiptUnitCost) }}/{{ selectedItem.baseUnit }}</strong></div><div><span>Current average</span><strong>{{ formatMoney(selectedItem.averageCostCentavos) }}/{{ selectedItem.baseUnit }}</strong></div><div class="result"><span>New weighted average</span><strong>{{ formatMoney(resultingAverage) }}/{{ selectedItem.baseUnit }}</strong></div></section>
      <div v-if="error" class="error-banner" role="alert">{{ error }}</div>
      <div class="form-actions"><router-link class="button ghost" :to="returnPath">Cancel</router-link><button class="button primary" :disabled="saving">{{ saving ? 'Saving receipt…' : 'Save stock receipt' }}</button></div>
    </form>
  </div>
</template>

<style scoped>
.money-input { position: relative; }.money-input span { position: absolute; left: 14px; top: 14px; color: var(--mashal-purple); font-weight: 800; }.money-input input { padding-left: 34px; }.field label i { font-style: normal; font-weight: 500; letter-spacing: 0; text-transform: none; }.preview { display: grid; gap: 12px; }.preview > div { display: flex; justify-content: space-between; gap: 16px; font-size: 12px; color: var(--mashal-muted); }.preview .result { padding-top: 12px; border-top: 1px solid rgba(91,44,131,.18); color: var(--mashal-navy); }.preview strong { color: var(--mashal-ink); }
</style>
