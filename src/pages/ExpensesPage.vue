<script setup lang="ts">
import { computed, ref } from 'vue'
import { IonIcon } from '@ionic/vue'
import { addOutline, trashOutline, createOutline, walletOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { centavosToPesos, localDateKey, pesosToCentavos } from '@/domain/calculations'
import { formatMoney, formatDate } from '@/composables/formatters'
import type { Expense } from '@/types/models'

const store = useBusinessStore()
const id = ref<string>()
const description = ref('')
const amount = ref(0)
const category = ref('Transportation')
const expenseDate = ref(localDateKey())
const saving = ref(false)
const error = ref('')
const sorted = computed(() => [...store.expenses].sort((a,b) => b.expenseDate.localeCompare(a.expenseDate) || b.createdAt.localeCompare(a.createdAt)))
const todayTotal = computed(() => store.expenses.filter((expense) => expense.expenseDate === localDateKey()).reduce((sum, expense) => sum + expense.amountCentavos, 0))
const categories = ['Transportation','Electricity','Water','Rent','Packaging','Supplies','Labor','Other']

function edit(expense: Expense): void { id.value = expense.id; description.value = expense.description; amount.value = centavosToPesos(expense.amountCentavos); category.value = expense.category; expenseDate.value = expense.expenseDate; document.querySelector<HTMLElement>('.app-shell')?.scrollTo({ top: 0, behavior: 'smooth' }) }
function reset(): void { id.value = undefined; description.value = ''; amount.value = 0; category.value = 'Transportation'; expenseDate.value = localDateKey() }
async function save(): Promise<void> {
  if (!description.value.trim() || amount.value <= 0) { error.value = 'Enter a description and amount.'; return }
  saving.value = true
  try { await store.saveExpense({ id: id.value, description: description.value, amountCentavos: pesosToCentavos(amount.value), category: category.value, expenseDate: expenseDate.value }); reset() }
  catch (reason) { error.value = reason instanceof Error ? reason.message : 'Unable to save expense.' }
  finally { saving.value = false }
}
</script>

<template>
  <div class="page"><header class="page-heading"><div><span class="eyebrow">Operating costs</span><h1>Expenses</h1><p>Keep daily costs simple and separate from product ingredients.</p></div><span class="date-chip">Today · {{ formatMoney(todayTotal) }}</span></header>
    <div class="split">
      <form class="card form-card expense-form" @submit.prevent="save"><div class="section-heading"><h2>{{ id ? 'Edit expense' : 'Add expense' }}</h2><button v-if="id" type="button" class="button ghost small" @click="reset">Cancel edit</button></div><div class="field"><label for="expense-description">Description</label><input id="expense-description" v-model="description" placeholder="e.g. Transportation" /></div><div class="field"><label for="expense-amount">Amount</label><div class="money-input"><span aria-hidden="true">₱</span><input id="expense-amount" v-model.number="amount" type="number" min="0" step="0.01" inputmode="decimal" /></div></div><div class="field"><label for="expense-category">Category</label><select id="expense-category" v-model="category"><option v-for="item in categories" :key="item">{{ item }}</option></select></div><div class="field"><label for="expense-date">Date</label><input id="expense-date" v-model="expenseDate" type="date" /></div><div v-if="error" class="error-banner" role="alert">{{ error }}</div><button class="button primary block" :disabled="saving"><ion-icon :icon="addOutline" aria-hidden="true" /> {{ saving ? 'Saving…' : id ? 'Update expense' : 'Save expense' }}</button></form>
      <section><div class="section-heading"><h2>Recent expenses</h2></div><div v-if="sorted.length" class="stack"><article v-for="expense in sorted" :key="expense.id" class="card list-card"><span class="list-icon"><ion-icon :icon="walletOutline" aria-hidden="true" /></span><span class="list-main"><strong>{{ expense.description }}</strong><small>{{ expense.category }} · {{ formatDate(expense.expenseDate) }}</small></span><strong>{{ formatMoney(expense.amountCentavos) }}</strong><div class="row-actions"><button type="button" :aria-label="`Edit ${expense.description} expense`" @click="edit(expense)"><ion-icon :icon="createOutline" aria-hidden="true" /></button><button type="button" class="delete" :aria-label="`Delete ${expense.description} expense`" @click="store.deleteExpense(expense.id)"><ion-icon :icon="trashOutline" aria-hidden="true" /></button></div></article></div><div v-else class="card empty"><span class="list-icon"><ion-icon :icon="walletOutline" aria-hidden="true" /></span><h3>No expenses recorded</h3><p>Add transport, utilities, labor, and other daily costs here.</p></div></section>
    </div>
  </div>
</template>

<style scoped>
.expense-form { align-self: start; }.money-input { position: relative; }.money-input span { position: absolute; left: 14px; top: 14px; color: var(--mashal-purple); font-weight: 800; }.money-input input { padding-left: 34px; }.row-actions { display: flex; gap: 6px; }.row-actions button { width: 44px; height: 44px; border: 0; border-radius: 11px; color: var(--mashal-purple); background: var(--mashal-purple-soft); }.row-actions button.delete { color: var(--mashal-danger); background: #fbe9e6; }.list-card { flex-wrap: wrap; }.list-card .list-main { min-width: 150px; }
</style>
