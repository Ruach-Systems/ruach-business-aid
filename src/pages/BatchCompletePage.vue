<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { IonIcon } from '@ionic/vue'
import { alertCircleOutline, checkmarkCircleOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { formatMoney, formatQuantity } from '@/composables/formatters'
import PageBackLink from '@/components/PageBackLink.vue'

const store = useBusinessStore()
const route = useRoute()
const router = useRouter()
const batch = computed(() => store.batches.find((candidate) => candidate.id === route.params.id))
const actualYield = ref(batch.value?.plannedYield || 0)
const actualQuantities = reactive<Record<string, number>>(Object.fromEntries((batch.value?.ingredients || []).map((line) => [line.inventoryItemId, line.actualQuantity])))
const note = ref(batch.value?.note || '')
const saving = ref(false)
const confirming = ref(false)
const error = ref('')
const totalCost = computed(() => (batch.value?.ingredients || []).reduce((sum, line) => sum + Math.round(Number(actualQuantities[line.inventoryItemId] || 0) * line.unitCostCentavos), 0))
const costPerUnit = computed(() => actualYield.value > 0 ? Math.round(totalCost.value / actualYield.value) : 0)
const yieldVariance = computed(() => actualYield.value - (batch.value?.plannedYield || 0))

async function complete(): Promise<void> {
  if (!batch.value || actualYield.value <= 0) { error.value = 'Enter the actual finished quantity.'; return }
  saving.value = true
  try { await store.completeBatch({ batchId: batch.value.id, actualYield: actualYield.value, actualQuantities, note: note.value }); await router.replace('/production') }
  catch (reason) { error.value = reason instanceof Error ? reason.message : 'Unable to complete batch.' }
  finally { saving.value = false }
}
</script>

<template>
  <div v-if="batch" class="page narrow"><PageBackLink :to="`/production/${batch.id}`" text="Back to batch draft" /><header class="page-heading"><div><span class="eyebrow">Final production record</span><h1>Complete batch</h1><p>{{ batch.productName }} · edit actual use before posting.</p></div></header>
    <form class="stack" @submit.prevent>
      <section class="card"><div class="section-heading"><div><span class="card-label">Actual ingredient use</span><h2>What was consumed?</h2></div></div><div class="actual-list"><div v-for="line in batch.ingredients" :key="line.inventoryItemId" class="actual-row"><div><strong>{{ line.itemName }}</strong><small>Planned {{ formatQuantity(line.plannedQuantity) }}</small></div><input v-model.number="actualQuantities[line.inventoryItemId]" type="number" min="0" step="0.01" inputmode="decimal" :aria-label="`Actual ${line.itemName} used`" /><span>{{ store.inventoryItems.find(item => item.id === line.inventoryItemId)?.baseUnit }}</span></div></div></section>
      <section class="card form-card"><div class="field"><label for="actual-yield">Actual finished yield</label><div class="field-row"><input id="actual-yield" v-model.number="actualYield" type="number" min="1" step="1" inputmode="numeric" /><input value="items" aria-label="Actual yield unit" disabled /></div></div><div class="field"><label for="completion-note">Completion note <i>optional</i></label><textarea id="completion-note" v-model="note"></textarea></div></section>
      <section class="card dark final-cost"><div><span class="card-label">Final batch cost</span><strong>{{ formatMoney(totalCost) }}</strong></div><div><span class="card-label">Cost per item</span><strong>{{ formatMoney(costPerUnit) }}</strong></div><div><span class="card-label">Yield variance</span><strong>{{ yieldVariance > 0 ? '+' : '' }}{{ yieldVariance }}</strong></div><footer><ion-icon :icon="checkmarkCircleOutline" /> Ingredients out · finished stock +{{ actualYield }}</footer></section>
      <div class="card warning irreversible"><ion-icon :icon="alertCircleOutline" /><div><strong>Completion is final</strong><span>MASHAL posts an immutable audit trail. Use a stock adjustment afterward if a physical correction is needed.</span></div></div>
      <section v-if="confirming" class="card confirmation-panel" role="region" aria-live="assertive" aria-labelledby="complete-confirmation-title"><span class="eyebrow">Ready to post</span><h2 id="complete-confirmation-title">Complete this batch?</h2><p>This will deduct the actual ingredients and add {{ actualYield }} finished items to stock.</p><div><button type="button" class="button ghost" @click="confirming = false">Keep editing</button><button type="button" class="button primary" :disabled="saving" @click="complete">{{ saving ? 'Posting movements…' : 'Confirm & post' }}</button></div></section>
      <div v-if="error" class="error-banner" role="alert">{{ error }}</div><div class="form-actions"><router-link class="button ghost" :to="`/production/${batch.id}`">Back to draft</router-link><button type="button" class="button primary" :disabled="saving" @click="confirming = true">Complete batch</button></div>
    </form>
  </div>
  <div v-else class="page narrow"><div class="card empty"><h3>Draft batch not found</h3><router-link class="button ghost small" to="/production">Back to production</router-link></div></div>
</template>

<style scoped>
.actual-list { display: grid; gap: 8px; }.actual-row { display: grid; grid-template-columns: minmax(0,1fr) 90px 48px; gap: 8px; align-items: center; padding: 11px; border-radius: 13px; background: var(--mashal-cloud); }.actual-row div { display: grid; gap: 3px; }.actual-row strong { font-size: 12px; }.actual-row small, .actual-row > span { color: var(--mashal-muted); font-size: 10px; }.actual-row input { min-width: 0; height: 44px; border-radius: 10px; border: 1px solid var(--mashal-line); background: white; padding: 8px; text-align: right; }.field label i { font-style: normal; font-weight: 500; letter-spacing: 0; text-transform: none; }.final-cost { display: grid; grid-template-columns: repeat(3,1fr); gap: 12px; }.final-cost > div { display: grid; gap: 6px; }.final-cost strong { font-size: 17px; }.final-cost div:first-child strong { color: var(--mashal-on-dark); }.final-cost footer { grid-column: 1/-1; display: flex; align-items: center; gap: 7px; padding-top: 12px; border-top: 1px solid rgba(255,255,255,.12); color: #CBD5E1; font-size: 11px; }.irreversible { display: flex; gap: 11px; }.irreversible > ion-icon { flex: none; font-size: 22px; color: var(--mashal-warning-ink); }.irreversible div { display: grid; gap: 5px; }.irreversible span { color: var(--mashal-muted); font-size: 11px; line-height: 1.45; }.confirmation-panel { display: grid; gap: 10px; border: 2px solid var(--mashal-purple); }.confirmation-panel h2 { font-family: var(--ion-font-family); color: var(--mashal-navy); margin: 0; }.confirmation-panel p { color: var(--mashal-muted); margin: 0; font-size: 12px; line-height: 1.5; }.confirmation-panel > div { display: grid; grid-template-columns: 1fr 1fr; gap: 9px; }
</style>
