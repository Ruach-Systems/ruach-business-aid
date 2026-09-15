<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { IonIcon } from '@ionic/vue'
import { useRoute, useRouter } from 'vue-router'
import { checkmarkCircleOutline, alertCircleOutline, chevronForwardOutline, restaurantOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { formatMoney, formatQuantity } from '@/composables/formatters'
import PageBackLink from '@/components/PageBackLink.vue'

const store = useBusinessStore()
const route = useRoute()
const router = useRouter()
const existing = computed(() => store.batches.find((batch) => batch.id === route.params.id))
const preparedProducts = computed(() => store.products.filter((product) => product.inventoryMode === 'prepared'))
const selectedProductId = ref(existing.value?.productId || '')
const step = ref<'select' | 'plan'>(existing.value ? 'plan' : 'select')
const plannedYield = ref(existing.value?.plannedYield || 20)
const note = ref(existing.value?.note || '')
const saving = ref(false)
const error = ref('')
const stepHeading = ref<HTMLHeadingElement | null>(null)
const selectedProduct = computed(() => preparedProducts.value.find((product) => product.id === selectedProductId.value))
const requirements = computed(() => (selectedProduct.value?.recipe || []).map((line) => {
  const item = store.inventoryItems.find((candidate) => candidate.id === line.inventoryItemId)
  const required = line.quantity * plannedYield.value
  return { ...line, item, required, ready: (item?.currentQuantity || 0) >= required, cost: Math.round(required * (item?.averageCostCentavos || 0)) }
}))
const plannedCost = computed(() => requirements.value.reduce((sum, line) => sum + line.cost, 0))

async function showStep(nextStep: 'select' | 'plan'): Promise<void> {
  step.value = nextStep
  await nextTick()
  document.querySelector<HTMLElement>('.app-shell')?.scrollTo({ top: 0, behavior: 'auto' })
  stepHeading.value?.focus({ preventScroll: true })
}
function continueToPlan(): void { if (selectedProductId.value) void showStep('plan') }
async function save(): Promise<void> {
  if (!selectedProduct.value || plannedYield.value <= 0) { error.value = 'Select a product and enter a planned yield.'; return }
  saving.value = true
  try { const batch = await store.saveBatch({ id: existing.value?.id, productId: selectedProduct.value.id, plannedYield: plannedYield.value, note: note.value }); await router.replace(`/production/${batch.id}`) }
  catch (reason) { error.value = reason instanceof Error ? reason.message : 'Unable to save batch.' }
  finally { saving.value = false }
}
</script>

<template>
  <div class="page narrow">
    <PageBackLink to="/production" text="Back to production" />
    <header class="page-heading"><div><span class="eyebrow">{{ step === 'select' ? 'Step 1 of 2' : existing ? 'Draft batch' : 'Step 2 of 2' }}</span><h1 ref="stepHeading" tabindex="-1">{{ step === 'select' ? 'Select a product' : existing ? `${existing.productName} plan` : 'Plan the batch' }}</h1><p>{{ step === 'select' ? 'Only prepared-in-advance products appear here.' : 'Nothing is deducted until you complete the batch.' }}</p></div></header>
    <template v-if="step === 'select'">
      <div v-if="preparedProducts.length" class="stack"><div class="stack" role="radiogroup" aria-label="Product to make"><button v-for="product in preparedProducts" :key="product.id" type="button" role="radio" :aria-checked="selectedProductId === product.id" :class="['card product-choice', { selected: selectedProductId === product.id }]" @click="selectedProductId = product.id"><span class="list-icon"><ion-icon :icon="restaurantOutline" aria-hidden="true" /></span><span class="list-main"><strong>{{ product.name }}</strong><small>{{ product.recipe.length }} recipe ingredients</small></span><ion-icon :icon="selectedProductId === product.id ? checkmarkCircleOutline : chevronForwardOutline" aria-hidden="true" /></button></div><button type="button" class="button primary block" :disabled="!selectedProductId" @click="continueToPlan">Continue to batch details</button></div>
      <div v-else class="card empty"><span class="list-icon"><ion-icon :icon="restaurantOutline" /></span><h3>No prepared products</h3><p>Create a product with the “Prepared in advance” inventory mode first.</p><router-link class="button primary small" to="/products/new">Create product</router-link></div>
    </template>
    <form v-else class="stack" @submit.prevent="save">
      <section class="card form-card"><div class="field"><label for="batch-product">Product</label><input id="batch-product" :value="selectedProduct?.name" disabled /></div><div class="field"><label for="planned-yield">Planned yield</label><div class="field-row"><input id="planned-yield" v-model.number="plannedYield" type="number" min="1" step="1" inputmode="numeric" /><input value="items" aria-label="Planned yield unit" disabled /></div></div><div class="field"><label for="batch-note">Batch note <i>optional</i></label><textarea id="batch-note" v-model="note" placeholder="e.g. Morning preparation"></textarea></div></section>
      <section class="card"><div class="section-heading"><div><span class="card-label">Live recipe calculation</span><h2>Required ingredients</h2></div><span class="pill">{{ formatMoney(plannedCost) }}</span></div><div v-if="requirements.length" class="requirement-list"><div v-for="line in requirements" :key="line.inventoryItemId" class="requirement"><ion-icon :class="{ warning: !line.ready }" :icon="line.ready ? checkmarkCircleOutline : alertCircleOutline" /><div><strong>{{ line.item?.name || 'Unknown item' }}</strong><small>{{ formatQuantity(line.required) }} {{ line.item?.baseUnit }} needed · {{ formatQuantity(line.item?.currentQuantity || 0) }} available</small></div><span>{{ formatMoney(line.cost) }}</span></div></div><div v-else class="error-banner">This product has no recipe ingredients.</div></section>
      <section class="card soft rule-note"><strong>Drafts do not change inventory</strong><span>Ingredient quantities are deducted and finished stock is added only after actual production is recorded.</span></section>
      <div v-if="error" class="error-banner" role="alert">{{ error }}</div><div class="form-actions"><button v-if="!existing" type="button" class="button ghost" @click="showStep('select')">Back to product selection</button><router-link v-else class="button ghost" to="/production">Cancel</router-link><button class="button primary" :disabled="saving || !requirements.length">{{ saving ? 'Saving…' : 'Save draft' }}</button></div>
      <router-link v-if="existing" class="button primary block" :to="`/production/${existing.id}/complete`">Record actuals & complete</router-link>
    </form>
  </div>
</template>

<style scoped>
.product-choice { width: 100%; text-align: left; color: inherit; display: flex; align-items: center; gap: 12px; }.product-choice > ion-icon { color: var(--mashal-muted); font-size: 22px; }.product-choice.selected { background: var(--mashal-purple-soft); border-color: var(--mashal-purple); }.product-choice.selected > ion-icon { color: var(--mashal-purple); }.field label i { font-style: normal; font-weight: 500; letter-spacing: 0; text-transform: none; }.requirement-list { display: grid; gap: 8px; }.requirement { padding: 12px; border-radius: 13px; background: var(--mashal-cloud); display: grid; grid-template-columns: 24px 1fr auto; align-items: center; gap: 9px; }.requirement > ion-icon { color: var(--mashal-success); font-size: 19px; }.requirement > ion-icon.warning { color: var(--mashal-warning-ink); }.requirement div { display: grid; gap: 3px; }.requirement strong { font-size: 12px; }.requirement small { color: var(--mashal-muted); font-size: 10px; }.requirement > span { font-size: 11px; font-weight: 800; }.rule-note { display: grid; gap: 5px; }.rule-note span { color: var(--mashal-muted); font-size: 11px; line-height: 1.5; }
</style>
