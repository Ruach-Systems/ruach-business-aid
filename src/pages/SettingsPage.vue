<script setup lang="ts">
import { IonIcon } from '@ionic/vue'
import { cloudOutline, logOutOutline, personCircleOutline } from 'ionicons/icons'
import { useRouter } from 'vue-router'
import { useBusinessStore } from '@/stores/business'
import { timeAgo } from '@/composables/formatters'

const store = useBusinessStore()
const router = useRouter()

async function logout(): Promise<void> { await store.logout(); await router.replace('/sign-in') }
</script>

<template>
  <div class="page narrow">
    <header class="page-heading"><div><span class="eyebrow">Preferences & safety</span><h1>Settings</h1><p>Manage your account, business details, and cloud sync.</p></div></header>
    <section class="card profile-card">
      <span class="profile-avatar"><img v-if="store.user?.photoURL" :src="store.user.photoURL" alt="" /><ion-icon v-else :icon="personCircleOutline" /></span>
      <div><strong>{{ store.user?.displayName }}</strong><span>{{ store.user?.email }}</span><small>Owner · one account, one business</small></div>
    </section>
    <section class="section stack">
      <article class="card">
        <div class="section-heading compact"><h2>Cloud sync</h2><span class="pill" :class="{ warning: store.syncStatus !== 'synced' }">{{ store.syncStatus }}</span></div>
        <p class="setting-copy">{{ store.isFirebaseConfigured ? `${timeAgo(store.lastSyncedAt)}. Firestore also keeps an offline cache on this device.` : 'Firebase is not configured. Your data is safely stored only in this browser.' }}</p>
        <button class="button ghost block" :disabled="!store.isFirebaseConfigured" @click="store.syncAll"><ion-icon :icon="cloudOutline" /> Sync now</button>
      </article>
      <article class="card">
        <div class="section-heading compact"><h2>Business</h2></div>
        <dl><div><dt>Name</dt><dd>{{ store.business?.name }}</dd></div><div><dt>Location</dt><dd>{{ store.business?.defaultLocation }}</dd></div><div><dt>Currency</dt><dd>Philippine peso (PHP)</dd></div><div><dt>Timezone</dt><dd>Asia/Manila</dd></div></dl>
      </article>
      <button class="button danger block" @click="logout"><ion-icon :icon="logOutOutline" /> Sign out</button>
    </section>
  </div>
</template>

<style scoped>
.profile-card { display: flex; align-items: center; gap: 15px; }
.profile-avatar { width: 54px; height: 54px; border-radius: 17px; overflow: hidden; display: grid; place-items: center; background: var(--mashal-purple-soft); color: var(--mashal-purple); flex: none; }
.profile-avatar img { width: 100%; height: 100%; object-fit: cover; }.profile-avatar ion-icon { font-size: 38px; }
.profile-card div { display: grid; gap: 3px; }.profile-card span { font-size: 12px; color: var(--mashal-muted); }.profile-card small { font-size: 10px; color: var(--mashal-purple); font-weight: 800; }
.section-heading.compact { margin-bottom: 10px; }.setting-copy { color: var(--mashal-muted); font-size: 12px; line-height: 1.6; margin: 0 0 16px; }
dl { margin: 0; display: grid; gap: 12px; } dl div { display: flex; justify-content: space-between; gap: 16px; border-bottom: 1px solid var(--mashal-line); padding-bottom: 10px; } dl div:last-child { border: 0; padding: 0; } dt { color: var(--mashal-muted); font-size: 12px; } dd { margin: 0; font-weight: 700; font-size: 12px; text-align: right; }
</style>
