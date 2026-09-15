<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useBusinessStore } from '@/stores/business'
import { formatQuantity } from '@/composables/formatters'
import PageBackLink from '@/components/PageBackLink.vue'

const store = useBusinessStore()
const route = useRoute()
const router = useRouter()
const sourceItemId = String(route.query.item || '')
const inventoryItemId = ref(sourceItemId || store.inventoryItems[0]?.id || '')
const countedQuantity = ref(0)
const reason = ref('Physical count')
const note = ref('')
const saving = ref(false)
const error = ref('')
const selectedItem = computed(() => store.inventoryItems.find((item) => item.id === inventoryItemId.value))
const returnPath = computed(() => sourceItemId ? `/inventory/${sourceItemId}` : '/inventory')
const returnLabel = computed(() => sourceItemId ? 'Back to inventory item' : 'Back to inventory')
const difference = computed(() => countedQuantity.value - (selectedItem.value?.currentQuantity || 0))
function itemChanged(): void { countedQuantity.value = selectedItem.value?.currentQuantity || 0 }
itemChanged()

async function save(): Promise<void> {
  if (!selectedItem.value || countedQuantity.value < 0) { error.value = 'Choose an item and enter the counted quantity.'; return }
  saving.value = true
  try { await store.adjustStock({ inventoryItemId: selectedItem.value.id, countedQuantity: countedQuantity.value, reason: reason.value, note: note.value }); await router.replace(`/inventory/${selectedItem.value.id}`) }
  catch (cause) { error.value = cause instanceof Error ? cause.message : 'Unable to save adjustment.' }
  finally { saving.value = false }
}
</script>

<template>
  <div class="page narrow"><PageBackLink :to="returnPath" :text="returnLabel" /><header class="page-heading"><div><span class="eyebrow">Audited correction</span><h1>Adjust stock</h1><p>Enter what you physically counted. MASHAL records the difference.</p></div></header>
    <form class="stack" @submit.prevent="save">
      <section class="card form-card">
        <div class="field"><label for="adjustment-item">Inventory item</label><select id="adjustment-item" v-model="inventoryItemId" required @change="itemChanged"><option disabled value="">Choose item</option><option v-for="item in store.inventoryItems" :key="item.id" :value="item.id">{{ item.name }}</option></select></div>
        <div v-if="selectedItem" class="current-stock"><span>Recorded balance</span><strong>{{ formatQuantity(selectedItem.currentQuantity) }} {{ selectedItem.baseUnit }}</strong></div>
        <div class="field"><label for="counted-quantity">Counted quantity</label><div class="field-row"><input id="counted-quantity" v-model.number="countedQuantity" type="number" min="0" step="0.01" inputmode="decimal" required /><input :value="selectedItem?.baseUnit || 'unit'" aria-label="Counted quantity unit" disabled /></div></div>
        <div v-if="selectedItem" class="difference" :class="{ negative: difference < 0 }"><span>Difference</span><strong>{{ difference > 0 ? '+' : '' }}{{ formatQuantity(difference) }} {{ selectedItem.baseUnit }}</strong><small>A movement will be created; history is never overwritten.</small></div>
        <div class="field"><label for="adjustment-reason">Reason</label><select id="adjustment-reason" v-model="reason"><option>Physical count</option><option>Waste / spoilage</option><option>Damage</option><option>Opening balance correction</option><option>Other</option></select></div>
        <div class="field"><label for="adjustment-note">Note <i>optional</i></label><textarea id="adjustment-note" v-model="note" placeholder="Explain the adjustment"></textarea></div>
      </section>
      <div v-if="error" class="error-banner" role="alert">{{ error }}</div><div class="form-actions"><router-link class="button ghost" :to="returnPath">Cancel</router-link><button class="button primary" :disabled="saving">{{ saving ? 'Saving…' : 'Save adjustment' }}</button></div>
    </form>
  </div>
</template>

<style scoped>
.current-stock, .difference { padding: 14px; border-radius: 14px; background: var(--mashal-cloud); display: flex; align-items: center; justify-content: space-between; gap: 12px; font-size: 12px; }.difference { display: grid; grid-template-columns: 1fr auto; background: var(--mashal-purple-soft); color: var(--mashal-purple); }.difference.negative { background: var(--mashal-warning-soft); color: var(--mashal-warning-ink); }.difference small { grid-column: 1/-1; color: var(--mashal-muted); }.field label i { font-style: normal; font-weight: 500; letter-spacing: 0; text-transform: none; }
</style>
