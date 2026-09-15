<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useBusinessStore } from '@/stores/business'
import { unitDefinitions, type UnitKind } from '@/types/models'
import PageBackLink from '@/components/PageBackLink.vue'

const store = useBusinessStore()
const router = useRouter()
const name = ref('')
const baseUnit = ref('pc')
const minimumQuantity = ref(0)
const saving = ref(false)
const error = ref('')
const kindFor = (code: string): UnitKind => unitDefinitions.find((unit) => unit.code === code)?.kind || 'custom'

async function save(): Promise<void> {
  if (!name.value.trim()) { error.value = 'Enter an item name.'; return }
  saving.value = true
  try { await store.saveInventoryItem({ name: name.value, baseUnit: baseUnit.value, unitKind: kindFor(baseUnit.value), minimumQuantity: minimumQuantity.value }); await router.replace('/inventory') }
  catch (reason) { error.value = reason instanceof Error ? reason.message : 'Unable to save item.' }
  finally { saving.value = false }
}
</script>

<template>
  <div class="page narrow"><PageBackLink to="/inventory" text="Back to inventory" /><header class="page-heading"><div><span class="eyebrow">Inventory setup</span><h1>New item</h1><p>Use the smallest practical unit for accurate recipes.</p></div></header>
    <form class="card form-card" @submit.prevent="save">
      <div class="field"><label for="inventory-name">Item name</label><input id="inventory-name" v-model="name" required autofocus placeholder="e.g. Condensed Milk" /></div>
      <div class="field"><label for="inventory-base-unit">Base unit</label><select id="inventory-base-unit" v-model="baseUnit" aria-describedby="base-unit-help"><option v-for="unit in unitDefinitions.filter(unit => unit.toBase === 1)" :key="unit.code" :value="unit.code">{{ unit.code }} · {{ unit.label }}</option></select><span id="base-unit-help" class="field-help">Purchases can still be entered in kg or L and will convert automatically.</span></div>
      <div class="field"><label for="inventory-minimum">Low-stock warning at</label><input id="inventory-minimum" v-model.number="minimumQuantity" type="number" min="0" step="0.01" inputmode="decimal" /></div>
      <div v-if="error" class="error-banner" role="alert">{{ error }}</div>
      <div class="form-actions"><router-link class="button ghost" to="/inventory">Cancel</router-link><button class="button primary" :disabled="saving">{{ saving ? 'Saving…' : 'Save item' }}</button></div>
    </form>
  </div>
</template>
