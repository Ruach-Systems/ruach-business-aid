<script setup lang="ts">
import { ref, computed } from 'vue'
import { IonIcon } from '@ionic/vue'
import { addOutline, optionsOutline, searchOutline, chevronForwardOutline, cubeOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { formatMoney, formatQuantity } from '@/composables/formatters'

const store = useBusinessStore()
const search = ref('')
const filtered = computed(() => store.inventoryItems.filter((item) => item.name.toLowerCase().includes(search.value.toLowerCase())))
</script>

<template>
  <div class="page">
    <header class="page-heading"><div><span class="eyebrow">Ingredients & finished stock</span><h1>Inventory</h1><p>Every balance is backed by a movement.</p></div><router-link class="button primary small" to="/inventory/new"><ion-icon :icon="addOutline" /> Add item</router-link></header>
    <div class="inventory-toolbar card"><div class="search-box"><ion-icon :icon="searchOutline" aria-hidden="true" /><label class="sr-only" for="inventory-search">Search inventory</label><input id="inventory-search" v-model="search" type="search" placeholder="Search inventory" /></div><div class="inventory-actions"><router-link class="button dark small" to="/stock/add"><ion-icon :icon="addOutline" aria-hidden="true" /> Add stock</router-link><router-link class="button ghost small" to="/stock/adjust"><ion-icon :icon="optionsOutline" aria-hidden="true" /> Adjust</router-link></div></div>
    <div v-if="filtered.length" class="inventory-grid section">
      <router-link v-for="item in filtered" :key="item.id" class="card list-card inventory-card" :to="`/inventory/${item.id}`">
        <span class="list-icon"><ion-icon :icon="cubeOutline" /></span>
        <span class="list-main"><span class="item-title"><strong>{{ item.name }}</strong><span v-if="item.currentQuantity <= item.minimumQuantity" class="pill warning">Low</span></span><small>Avg. {{ formatMoney(item.averageCostCentavos) }}/{{ item.baseUnit }}</small></span>
        <span class="quantity"><strong>{{ formatQuantity(item.currentQuantity) }}</strong><small>{{ item.baseUnit }}</small></span>
        <ion-icon class="chevron" :icon="chevronForwardOutline" />
      </router-link>
    </div>
    <div v-else class="card empty section"><span class="list-icon"><ion-icon :icon="cubeOutline" /></span><h3>{{ search ? 'No matching items' : 'No inventory items yet' }}</h3><p>Create raw ingredients, packaging, or finished products to start tracking stock.</p><router-link v-if="!search" class="button primary small" to="/inventory/new">Add first item</router-link></div>
  </div>
</template>

<style scoped>
.inventory-toolbar { display: grid; gap: 12px; }.search-box { display: flex; align-items: center; gap: 9px; background: var(--mashal-cloud); border-radius: 13px; padding: 0 13px; }.search-box:focus-within { box-shadow: 0 0 0 3px var(--mashal-focus); }.search-box ion-icon { color: var(--mashal-muted); }.search-box input { flex: 1; min-width: 0; min-height: 44px; border: 0; background: transparent; outline: none; }.inventory-actions { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }.inventory-grid { display: grid; gap: 10px; }.item-title { display: flex; align-items: center; gap: 8px; }.quantity { display: grid; text-align: right; }.quantity small { color: var(--mashal-muted); font-size: 10px; }.inventory-card .chevron { display: none; }
.search-box:focus-within { box-shadow: 0 0 0 3px var(--mashal-focus); }
@media (min-width: 700px) { .inventory-toolbar { grid-template-columns: 1fr auto; align-items: center; }.inventory-grid { grid-template-columns: repeat(2,minmax(0,1fr)); }.inventory-card .chevron { display: block; } }
</style>
