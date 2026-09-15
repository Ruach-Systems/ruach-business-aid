<script setup lang="ts">
import { computed } from 'vue'
import { IonIcon } from '@ionic/vue'
import { arrowForwardOutline, cartOutline, cubeOutline, restaurantOutline, walletOutline, alertCircleOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { formatDate, formatMoney } from '@/composables/formatters'
import { currentSalesTotals } from '@/domain/calculations'

const store = useBusinessStore()
const todayLabel = new Intl.DateTimeFormat('en-PH', { weekday: 'long', month: 'long', day: 'numeric' }).format(new Date())
const latestSales = computed(() => [...store.sales].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).slice(0, 4))
const salesTotals = computed(() => currentSalesTotals(store.sales, store.expenses))
const monthLabel = new Intl.DateTimeFormat('en-PH', { month: 'long' }).format(new Date())
</script>

<template>
  <div class="page">
    <header class="page-heading">
      <div><span class="eyebrow">Today at a glance</span><h1>Business overview</h1><p>{{ store.business?.name }} · {{ store.business?.defaultLocation }}</p></div>
      <span class="date-chip">{{ todayLabel }}</span>
    </header>

    <section class="today-summary" aria-label="Today's performance">
      <article class="card dark profit-card">
        <span class="card-label">Estimated profit</span>
        <div class="metric-value">{{ formatMoney(store.todayMetrics.profitCentavos) }}</div>
        <span class="metric-note">Revenue − product cost − expenses</span>
        <div class="profit-decoration" aria-hidden="true"></div>
      </article>
      <article class="card today-breakdown">
        <div class="today-breakdown-heading">
          <span class="card-label">Today's activity</span>
          <span class="item-count">{{ store.todayMetrics.itemCount }} item{{ store.todayMetrics.itemCount === 1 ? '' : 's' }} sold</span>
        </div>
        <div class="today-total"><span>Sales</span><strong>{{ formatMoney(store.todayMetrics.revenueCentavos) }}</strong></div>
        <div class="today-total"><span>Product cost</span><strong>{{ formatMoney(store.todayMetrics.costCentavos) }}</strong></div>
        <div class="today-total"><span>Expenses</span><strong>{{ formatMoney(store.todayMetrics.expenseCentavos) }}</strong></div>
      </article>
    </section>

    <section class="section">
      <div class="section-heading"><h2>Quick actions</h2></div>
      <div class="quick-actions">
        <router-link class="button primary" to="/sales"><ion-icon :icon="cartOutline" /> Record sale</router-link>
        <router-link class="button ghost" to="/production/new"><ion-icon :icon="restaurantOutline" /> Make batch</router-link>
        <router-link class="button ghost" to="/stock/add"><ion-icon :icon="cubeOutline" /> Add stock</router-link>
        <router-link class="button ghost" to="/expenses"><ion-icon :icon="walletOutline" /> Add expense</router-link>
      </div>
    </section>

    <section class="section">
      <div class="section-heading"><div><span class="section-kicker">Performance</span><h2>Week and month to date</h2></div></div>
      <div class="period-sales-grid">
        <article class="card period-sales-card">
          <span class="card-label">This week</span>
          <div class="period-total"><span>Sales</span><strong>{{ formatMoney(salesTotals.weeklyRevenueCentavos) }}</strong></div>
          <div class="period-total"><span>Expenses</span><strong>{{ formatMoney(salesTotals.weeklyExpenseCentavos) }}</strong></div>
          <div :class="['period-total', 'profit', { negative: salesTotals.weeklyProfitCentavos < 0 }]"><span>Estimated profit</span><strong>{{ formatMoney(salesTotals.weeklyProfitCentavos) }}</strong></div>
          <span class="metric-note">Monday through today · after product cost</span>
        </article>
        <article class="card period-sales-card">
          <span class="card-label">This month</span>
          <div class="period-total"><span>Sales</span><strong>{{ formatMoney(salesTotals.monthlyRevenueCentavos) }}</strong></div>
          <div class="period-total"><span>Expenses</span><strong>{{ formatMoney(salesTotals.monthlyExpenseCentavos) }}</strong></div>
          <div :class="['period-total', 'profit', { negative: salesTotals.monthlyProfitCentavos < 0 }]"><span>Estimated profit</span><strong>{{ formatMoney(salesTotals.monthlyProfitCentavos) }}</strong></div>
          <span class="metric-note">{{ monthLabel }} to date · after product cost</span>
        </article>
      </div>
    </section>

    <div class="split section">
      <section>
        <div class="section-heading"><h2>Recent sales</h2><router-link to="/sales">View all <ion-icon :icon="arrowForwardOutline" /></router-link></div>
        <div v-if="latestSales.length" class="stack">
          <article v-for="sale in latestSales" :key="sale.id" class="card list-card">
            <span class="list-icon"><ion-icon :icon="cartOutline" /></span>
            <div class="list-main">
              <strong>{{ sale.totalItems }} item{{ sale.totalItems === 1 ? '' : 's' }} · {{ sale.location }}</strong>
              <small>{{ formatDate(sale.saleDate) }} · {{ sale.lines.map(line => line.productName).join(', ') }}</small>
            </div>
            <strong class="sale-amount">{{ formatMoney(sale.totalRevenueCentavos) }}</strong>
          </article>
        </div>
        <div v-else class="card empty"><span class="list-icon"><ion-icon :icon="cartOutline" /></span><h3>No sales recorded yet</h3><p>Record your first sale and MASHAL will calculate revenue, cost, and profit.</p><router-link class="button primary small" to="/sales">Record first sale</router-link></div>
      </section>
      <section>
        <div class="section-heading"><h2>Low stock</h2><router-link to="/inventory">View all</router-link></div>
        <div v-if="store.inventoryItems.length && store.lowStockItems.length" class="card warning stack">
          <div v-for="item in store.lowStockItems.slice(0, 5)" :key="item.id" class="list-card compact">
            <span class="list-icon warning"><ion-icon :icon="alertCircleOutline" /></span>
            <div class="list-main"><strong>{{ item.name }}</strong><small>{{ item.currentQuantity }} {{ item.baseUnit }} remaining</small></div>
          </div>
        </div>
        <div v-else-if="store.inventoryItems.length" class="card success"><span class="card-label">Inventory health</span><h3 class="healthy">Everything looks stocked</h3><p class="metric-note">No items are at or below their minimum.</p></div>
        <div v-else class="card empty compact-empty"><span class="list-icon"><ion-icon :icon="cubeOutline" /></span><h3>No inventory to monitor</h3><p>Add an inventory item to start tracking stock levels.</p><router-link class="button ghost small" to="/inventory/new">Add inventory item</router-link></div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.today-summary { display: grid; gap: 12px; }
.profit-card { min-height: 180px; position: relative; overflow: hidden; display: flex; flex-direction: column; justify-content: center; }
.profit-decoration { width: 180px; height: 180px; border-radius: 50%; position: absolute; right: -70px; bottom: -100px; border: 24px solid rgba(255,255,255,.05); box-shadow: 0 0 0 26px rgba(255,255,255,.025); }
.today-breakdown { display: grid; align-content: center; }
.today-breakdown-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 5px; }
.item-count { color: var(--mashal-muted); font-size: 11px; }
.today-total { display: flex; align-items: baseline; justify-content: space-between; gap: 14px; padding: 10px 0; border-bottom: 1px solid var(--mashal-line); color: var(--mashal-muted); font-size: 13px; }
.today-total:last-child { border-bottom: 0; }
.today-total strong { color: var(--mashal-navy); font-size: 17px; text-align: right; overflow-wrap: anywhere; }
.section-kicker { display: block; margin-bottom: 4px; color: var(--mashal-purple); font-size: 10px; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
.period-sales-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.period-sales-card { min-width: 0; }
.period-sales-card .card-label { display: block; margin-bottom: 10px; }
.period-total { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; padding: 7px 0; color: var(--mashal-muted); }
.period-total strong { color: var(--mashal-navy); font-size: 16px; overflow-wrap: anywhere; text-align: right; }
.period-total.profit { border-top: 1px solid var(--mashal-border); margin-top: 4px; padding-top: 11px; }
.period-total.profit span, .period-total.profit strong { color: var(--mashal-success-ink); font-weight: 800; }
.period-total.profit.negative span, .period-total.profit.negative strong { color: var(--mashal-danger); }
.period-sales-card .metric-note { display: block; margin-top: 7px; }
.section-heading a { display: inline-flex; align-items: center; gap: 3px; }
.sale-amount { flex: none; margin: 0 !important; text-align: right; }
.list-icon.warning { background: var(--mashal-warning-soft); color: var(--mashal-warning-ink); }
.list-card.compact { min-height: 54px; padding: 0; }
.compact-empty { padding: 26px 18px; }
.healthy { margin: 9px 0 6px !important; color: var(--mashal-navy) !important; }
@media (min-width: 760px) { .today-summary { grid-template-columns: minmax(0, 1.1fr) minmax(320px, .9fr); } }
@media (max-width: 420px) {
  .period-sales-grid { grid-template-columns: 1fr; }
  .list-card { align-items: flex-start; flex-wrap: wrap; }
  .list-card .sale-amount { width: 100%; padding-left: 54px; text-align: left; }
}
</style>
