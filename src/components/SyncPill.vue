<script setup lang="ts">
import { computed } from 'vue'
import { cloudDoneOutline, cloudOfflineOutline, refreshOutline, warningOutline } from 'ionicons/icons'
import { IonIcon } from '@ionic/vue'
import { useBusinessStore } from '@/stores/business'
import { timeAgo } from '@/composables/formatters'

const store = useBusinessStore()
const icon = computed(() => ({ synced: cloudDoneOutline, syncing: refreshOutline, error: warningOutline, local: cloudOfflineOutline }[store.syncStatus]))
const label = computed(() => store.syncStatus === 'synced' ? timeAgo(store.lastSyncedAt) : store.syncStatus === 'syncing' ? 'Syncing…' : store.syncStatus === 'error' ? 'Sync needs attention' : 'Saved offline')
</script>

<template>
  <button class="sync-pill" :class="`sync-${store.syncStatus}`" type="button" :aria-label="`${label}. Sync now`" @click="store.syncAll">
    <ion-icon :icon="icon" :class="{ spinning: store.syncStatus === 'syncing' }" aria-hidden="true" />
    <span aria-live="polite">{{ label }}</span>
  </button>
</template>
