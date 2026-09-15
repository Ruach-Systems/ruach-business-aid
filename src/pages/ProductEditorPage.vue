<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { IonIcon } from '@ionic/vue'
import { addOutline, closeOutline, cubeOutline, removeCircleOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import PageBackLink from '@/components/PageBackLink.vue'
import { centavosToPesos, grossMarginPercent, markupPercent, pesosToCentavos, productCostCentavos } from '@/domain/calculations'
import { formatMoney } from '@/composables/formatters'
import type { ProductInventoryMode, RecipeLine } from '@/types/models'

const store = useBusinessStore()
const route = useRoute()
const router = useRouter()
const existing = computed(() => store.products.find((product) => product.id === route.params.id))
const name = ref(existing.value?.name || '')
const price = ref(centavosToPesos(existing.value?.sellingPriceCentavos || 0))
const manualCost = ref(centavosToPesos(existing.value?.manualCostCentavos || 0))
const mode = ref<ProductInventoryMode>(existing.value?.inventoryMode || 'untracked')
const recipe = ref<RecipeLine[]>(existing.value?.recipe.map((line) => ({ ...line })) || [])
const active = ref(existing.value?.isActive ?? true)
const saving = ref(false)
const error = ref('')

const draftProduct = computed(() => ({
  ...(existing.value || {}), inventoryMode: mode.value, manualCostCentavos: pesosToCentavos(manualCost.value),
  recipe: recipe.value, finishedInventoryItemId: existing.value?.finishedInventoryItemId
} as any))
const cost = computed(() => productCostCentavos(draftProduct.value, store.inventoryItems))
const profit = computed(() => pesosToCentavos(price.value) - cost.value)

function addRecipeLine(): void { recipe.value.push({ inventoryItemId: store.inventoryItems[0]?.id || '', quantity: 1 }) }
function removeRecipeLine(index: number): void { recipe.value.splice(index, 1) }

async function save(): Promise<void> {
  if (!name.value.trim() || price.value <= 0) { error.value = 'Enter a product name and selling price.'; return }
  if (mode.value !== 'untracked' && recipe.value.some((line) => !line.inventoryItemId || line.quantity <= 0)) { error.value = 'Complete every recipe line.'; return }
  saving.value = true
  try {
    await store.saveProduct({
      id: existing.value?.id, name: name.value, sellingPriceCentavos: pesosToCentavos(price.value),
      manualCostCentavos: pesosToCentavos(manualCost.value), inventoryMode: mode.value,
      finishedInventoryItemId: existing.value?.finishedInventoryItemId, recipe: mode.value === 'untracked' ? [] : recipe.value,
      isActive: active.value
    })
    await router.replace('/products')
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'Unable to save product.' }
  finally { saving.value = false }
}
</script>

<template>
  <div class="page narrow">
    <PageBackLink to="/products" text="Back to products" />
    <header class="page-heading"><div><span class="eyebrow">Products & costing</span><h1>{{ existing ? 'Edit product' : 'New product' }}</h1><p>Price and cost stay separate so profit remains clear.</p></div></header>
    <form class="stack" @submit.prevent="save">
      <section class="card form-card">
        <div class="field"><label for="product-name">Product name</label><input id="product-name" v-model="name" required placeholder="e.g. Buko Juice" /></div>
        <div class="field"><label for="product-price">Selling price</label><div class="money-input"><span aria-hidden="true">₱</span><input id="product-price" v-model.number="price" type="number" min="0" step="0.01" inputmode="decimal" required /></div></div>
        <div class="field"><span id="inventory-behavior-label" class="field-label">Inventory behavior</span><div class="mode-grid" role="radiogroup" aria-labelledby="inventory-behavior-label">
          <button type="button" role="radio" :aria-checked="mode === 'untracked'" :class="['mode-card', { active: mode === 'untracked' }]" @click="mode = 'untracked'"><ion-icon :icon="removeCircleOutline" aria-hidden="true" /><strong>Not tracked</strong><small>Financial sale only.</small></button>
          <button type="button" role="radio" :aria-checked="mode === 'prepared'" :class="['mode-card', { active: mode === 'prepared' }]" @click="mode = 'prepared'"><ion-icon :icon="cubeOutline" aria-hidden="true" /><strong>Prepared in advance</strong><small>Batch creates finished stock.</small></button>
        </div></div>
        <div v-if="mode === 'untracked'" class="field"><label for="manual-product-cost">Manual cost per item</label><div class="money-input"><span aria-hidden="true">₱</span><input id="manual-product-cost" v-model.number="manualCost" type="number" min="0" step="0.01" inputmode="decimal" /></div></div>
      </section>

      <section v-if="mode !== 'untracked'" class="card">
        <div class="section-heading"><div><span class="card-label">Recipe per one item</span><h2>Ingredients</h2></div><button type="button" class="button ghost small" :disabled="!store.inventoryItems.length" @click="addRecipeLine"><ion-icon :icon="addOutline" /> Add</button></div>
        <p v-if="!store.inventoryItems.length" class="field-help">Create inventory items first, then return to build the recipe.</p>
        <div v-if="recipe.length" class="recipe-list">
          <div v-for="(line, index) in recipe" :key="index" class="recipe-row">
            <select v-model="line.inventoryItemId" :aria-label="`Ingredient ${index + 1}`"><option disabled value="">Select ingredient</option><option v-for="item in store.inventoryItems" :key="item.id" :value="item.id">{{ item.name }}</option></select>
            <input v-model.number="line.quantity" type="number" min="0" step="0.01" inputmode="decimal" :aria-label="`Quantity for ingredient ${index + 1}`" />
            <span>{{ store.inventoryItems.find(item => item.id === line.inventoryItemId)?.baseUnit || 'unit' }}</span>
            <button type="button" :aria-label="`Remove ingredient ${index + 1}`" @click="removeRecipeLine(index)"><ion-icon :icon="closeOutline" aria-hidden="true" /></button>
          </div>
        </div>
        <div v-else class="empty compact"><p>No recipe ingredients added.</p></div>
      </section>

      <section class="card dark cost-preview">
        <div><span class="card-label">Selling price</span><strong>{{ formatMoney(pesosToCentavos(price)) }}</strong></div>
        <div><span class="card-label">Product cost</span><strong>{{ formatMoney(cost) }}</strong></div>
        <div class="highlight"><span class="card-label">Gross profit</span><strong>{{ formatMoney(profit) }}</strong></div>
        <footer><span>Margin {{ grossMarginPercent(pesosToCentavos(price), cost).toFixed(1) }}%</span><span>Markup {{ markupPercent(cost, pesosToCentavos(price)).toFixed(1) }}%</span></footer>
      </section>
      <label class="toggle-row"><span><strong>Product is active</strong><small>Inactive products no longer appear in sales.</small></span><input v-model="active" class="toggle" type="checkbox" /></label>
      <div v-if="error" class="error-banner" role="alert">{{ error }}</div>
      <div class="form-actions"><router-link class="button ghost" to="/products">Cancel</router-link><button class="button primary" :disabled="saving">{{ saving ? 'Saving…' : 'Save product' }}</button></div>
    </form>
  </div>
</template>

<style scoped>
.money-input { position: relative; }.money-input span { position: absolute; left: 14px; top: 14px; font-weight: 800; color: var(--mashal-purple); }.money-input input { padding-left: 34px; }.mode-grid { display: grid; gap: 8px; }.mode-card { text-align: left; border: 1px solid var(--mashal-line); background: var(--mashal-cloud); border-radius: 14px; padding: 13px; display: grid; grid-template-columns: 28px 1fr; align-items: center; color: var(--mashal-ink); }.mode-card ion-icon { grid-row: span 2; color: var(--mashal-purple); font-size: 20px; }.mode-card strong { font-size: 12px; }.mode-card small { color: var(--mashal-muted); font-size: 10px; margin-top: 3px; }.mode-card.active { border-color: var(--mashal-purple); background: var(--mashal-purple-soft); box-shadow: inset 0 0 0 1px var(--mashal-purple); }.recipe-list { display: grid; gap: 8px; }.recipe-row { display: grid; grid-template-columns: minmax(0,1fr) 74px 48px 44px; gap: 7px; align-items: center; }.recipe-row select, .recipe-row input { min-height: 44px; border: 1px solid var(--mashal-line); background: var(--mashal-cloud); border-radius: 11px; padding: 8px; min-width: 0; }.recipe-row > span { color: var(--mashal-muted); font-size: 11px; }.recipe-row button { width: 44px; height: 44px; border: 0; border-radius: 11px; background: #fbe9e6; color: var(--mashal-danger); }.empty.compact { padding: 14px; }.cost-preview { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }.cost-preview > div { display: grid; gap: 7px; }.cost-preview strong { font-size: 17px; }.cost-preview .highlight strong { color: var(--mashal-on-dark); }.cost-preview footer { grid-column: 1/-1; display: flex; justify-content: space-between; color: #CBD5E1; font-size: 11px; padding-top: 12px; border-top: 1px solid rgba(255,255,255,.12); }
@media (min-width: 600px) { .mode-grid { grid-template-columns: repeat(2, 1fr); }.mode-card { grid-template-columns: 1fr; gap: 5px; }.mode-card ion-icon { grid-row: auto; } }
</style>
