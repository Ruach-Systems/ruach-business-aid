<script setup lang="ts">
import BrandLogo from '@/components/BrandLogo.vue'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useBusinessStore } from '@/stores/business'
import { IonIcon } from '@ionic/vue'
import { logOutOutline } from 'ionicons/icons'

const store = useBusinessStore()
const router = useRouter()
const name = ref('')
const location = ref('Main Location')
const includeSamples = ref(true)
const saving = ref(false)
const leaving = ref(false)
const resetting = ref(false)
const error = ref('')
const accountError = ref('')
const resetError = ref('')
const showResetSignIn = import.meta.env.VITE_ENABLE_SIGNIN_RESET === 'true'

async function submit(): Promise<void> {
  if (!name.value.trim()) { error.value = 'Enter your business name.'; return }
  saving.value = true
  try {
    await store.createBusiness(name.value, location.value, includeSamples.value)
    await router.replace('/dashboard')
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'Unable to create business.' }
  finally { saving.value = false }
}

async function leaveOnboarding(): Promise<void> {
  accountError.value = ''
  leaving.value = true
  try {
    await store.logout()
    await router.replace('/sign-in')
  } catch {
    accountError.value = 'Could not sign out. Please check your connection and try again.'
  } finally {
    leaving.value = false
  }
}

async function resetSignIn(): Promise<void> {
  resetError.value = ''
  resetting.value = true
  try {
    await store.resetSignedInUser()
    await router.replace('/sign-in')
  } catch {
    resetError.value = 'Could not reset sign-in session. Please refresh and try again.'
  } finally {
    resetting.value = false
  }
}
</script>

<template>
  <main class="onboarding-page">
    <section class="onboarding-card">
      <BrandLogo />
      <span class="eyebrow">First things first</span>
      <h1>Create your business</h1>
      <p>One account can manage one business. You can change these details later.</p>
      <div v-if="store.user" class="account-summary">
        <span>Signed in as</span>
        <strong>{{ store.user.email }}</strong>
      </div>
      <form class="form-card" @submit.prevent="submit">
        <div class="field"><label for="business-name">Business name</label><input id="business-name" v-model="name" required autofocus placeholder="e.g. Aling Nena's Kakanin" /></div>
        <div class="field"><label for="business-location">Default location</label><input id="business-location" v-model="location" required /></div>
        <div class="info-panel"><strong>Philippine peso · Asia/Manila</strong><span>Your reporting day and currency are already configured.</span></div>
        <label class="toggle-row">
          <span><strong>Add sample products</strong><small>Buko Juice, Empanada, Siomai, and recipe ingredients</small></span>
          <input v-model="includeSamples" class="toggle" type="checkbox" />
        </label>
        <div v-if="error" class="error-banner" role="alert">{{ error }}</div>
        <button class="button primary block" :disabled="saving">{{ saving ? 'Creating…' : 'Create business' }}</button>
      </form>
      <div class="account-actions">
        <button class="button ghost block" type="button" :disabled="saving || leaving" @click="leaveOnboarding">
          <ion-icon :icon="logOutOutline" /> {{ leaving ? 'Signing out…' : 'Cancel and use a different account' }}
        </button>
        <small v-if="accountError" class="account-message" role="alert">{{ accountError }}</small>
      </div>
      <div v-if="showResetSignIn" class="debug-actions">
        <button v-if="!resetting" class="button ghost block" type="button" :disabled="saving" @click="resetSignIn">
          <ion-icon :icon="logOutOutline" /> Reset sign-in state
        </button>
        <div v-else class="error-banner" role="status">Resetting sign-in…</div>
        <small v-if="resetError" class="debug-message" role="alert">{{ resetError }}</small>
      </div>
    </section>
  </main>
</template>

<style scoped>
.onboarding-page { height: 100%; min-height: 100dvh; overflow-x: hidden; overflow-y: auto; overscroll-behavior-y: contain; -webkit-overflow-scrolling: touch; display: flex; align-items: flex-start; justify-content: center; padding: max(26px, env(safe-area-inset-top)) max(18px, env(safe-area-inset-right)) max(26px, env(safe-area-inset-bottom)) max(18px, env(safe-area-inset-left)); background: var(--mashal-navy); }
.onboarding-card { width: min(100%, 560px); min-width: 0; flex: none; background: var(--mashal-paper); border-radius: 28px; padding: clamp(26px, 6vw, 44px); box-shadow: var(--mashal-shadow); }
h1 { max-width: 100%; font-family: var(--ion-font-family); color: var(--mashal-navy); font-size: clamp(30px, 8vw, 38px); line-height: 1.12; margin: 10px 0 8px; overflow-wrap: anywhere; }
section > p { color: var(--mashal-muted); margin: 0 0 28px; line-height: 1.5; }
.onboarding-card > .eyebrow { display: block; margin-top: 30px; }
.account-summary { margin: -10px 0 24px; padding: 12px 14px; border: 1px solid var(--mashal-line); border-radius: 14px; display: grid; gap: 3px; }
.account-summary span { color: var(--mashal-muted); font-size: 11px; }
.account-summary strong { color: var(--mashal-navy); font-size: 13px; overflow-wrap: anywhere; }
.info-panel { padding: 14px; border-radius: 14px; background: var(--mashal-purple-soft); display: grid; gap: 4px; }
.info-panel strong { font-size: 12px; color: var(--mashal-navy); }
.info-panel span { color: var(--mashal-muted); font-size: 11px; }
.toggle-row > span { min-width: 0; }
.toggle-row small { display: block; line-height: 1.4; overflow-wrap: anywhere; }
.onboarding-card .button { height: auto; min-width: 0; padding-top: 12px; padding-bottom: 12px; white-space: normal; text-align: center; line-height: 1.35; }
.onboarding-card :is(input, button) { max-width: 100%; }
.account-actions { margin-top: 14px; display: grid; gap: 8px; }
.account-message { color: var(--mashal-danger); font-size: 11px; line-height: 1.4; text-align: center; }
.debug-actions { margin-top: 14px; display: grid; gap: 8px; }
.debug-message { color: var(--mashal-danger); font-size: 11px; line-height: 1.4; text-align: center; }
@media (max-width: 359px) {
  .onboarding-page { padding: max(12px, env(safe-area-inset-top)) 10px max(12px, env(safe-area-inset-bottom)); }
  .onboarding-card { border-radius: 20px; padding: 22px 18px; }
  .onboarding-card > .eyebrow { margin-top: 24px; }
  section > p { margin-bottom: 22px; }
  .toggle-row { align-items: flex-start; gap: 12px; padding: 12px; }
}
@media (max-height: 600px) and (orientation: landscape) {
  .onboarding-page { padding-top: 14px; padding-bottom: 14px; }
  .onboarding-card { padding-top: 24px; padding-bottom: 24px; }
  .onboarding-card > .eyebrow { margin-top: 22px; }
}
</style>
