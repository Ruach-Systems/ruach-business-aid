<script setup lang="ts">
import BrandLogo from '@/components/BrandLogo.vue'
import { ref } from 'vue'
import { IonIcon } from '@ionic/vue'
import { logoGoogle, cloudOfflineOutline, shieldCheckmarkOutline, logOutOutline } from 'ionicons/icons'
import { useBusinessStore } from '@/stores/business'
import { authErrorMessage } from '@/services/authErrors'
import { useRouter } from 'vue-router'

const store = useBusinessStore()
const busy = ref(false)
const resetting = ref(false)
const error = ref('')
const resetError = ref('')
const router = useRouter()
const showResetSignIn = import.meta.env.VITE_ENABLE_SIGNIN_RESET === 'true'

async function signIn(): Promise<void> {
  busy.value = true
  error.value = ''
  try { await store.loginWithGoogle() }
  catch (reason) { error.value = authErrorMessage(reason) }
  finally { busy.value = false }
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
  <main class="auth-page">
    <section class="auth-story">
      <div class="auth-brand"><BrandLogo reversed /><span class="product-name">Business Aid</span></div>
      <div>
        <span class="eyebrow">Daily business, made lighter</span>
        <h1>Know what you made.<br><em>Every day.</em></h1>
        <p>Sales, costs, expenses, inventory, and production—kept simple enough to use while you are busy serving customers.</p>
      </div>
      <div class="auth-benefits">
        <span><ion-icon :icon="cloudOfflineOutline" /> Works offline</span>
        <span><ion-icon :icon="shieldCheckmarkOutline" /> Your business stays private</span>
      </div>
    </section>
    <section class="auth-panel">
      <div class="auth-card">
        <span class="eyebrow">Welcome to Mashal Business Aid</span>
        <h2>Sign in to your business</h2>
        <p>One Google account securely creates and manages one business.</p>
        <div v-if="error || store.errorMessage" class="error-banner" role="alert">{{ error || store.errorMessage }}</div>
        <button v-if="store.isFirebaseConfigured" class="button primary google block" :disabled="busy" @click="signIn">
          <ion-icon :icon="logoGoogle" /> {{ busy ? 'Opening Google…' : 'Continue with Google' }}
        </button>
        <template v-else>
          <div class="setup-note">
            <strong>Firebase setup required for Google sign-in</strong>
            <span>Add your free Firebase web configuration to <code>.env</code>. You can explore everything locally now.</span>
          </div>
          <button class="button dark block" @click="store.continueLocally">
            <ion-icon :icon="cloudOfflineOutline" /> Continue locally
          </button>
        </template>
        <small class="auth-fineprint">After your first online sign-in, MASHAL remains available offline on this device.</small>
        <div v-if="showResetSignIn" class="debug-actions">
          <button v-if="!resetting" class="button ghost block" type="button" @click="resetSignIn">
            <ion-icon :icon="logOutOutline" /> Reset sign-in state
          </button>
          <div v-else class="error-banner" role="status">Resetting sign-in…</div>
          <small v-if="resetError" class="debug-message" role="alert">{{ resetError }}</small>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.auth-page { height: 100%; min-height: 100dvh; overflow-x: hidden; overflow-y: auto; overscroll-behavior-y: contain; -webkit-overflow-scrolling: touch; display: grid; align-content: start; background: var(--mashal-cloud); }
.auth-story { min-height: 44dvh; padding: max(32px, env(safe-area-inset-top)) max(26px, env(safe-area-inset-right)) 32px max(26px, env(safe-area-inset-left)); color: white; background: var(--mashal-navy); display: flex; flex-direction: column; justify-content: space-between; gap: 46px; position: relative; overflow: hidden; }
.auth-story::after { content: ''; width: 360px; height: 360px; border: 1px solid rgba(255,255,255,.10); border-radius: 50%; position: absolute; right: -190px; bottom: -180px; box-shadow: 0 0 0 60px rgba(255,255,255,.025), 0 0 0 120px rgba(255,255,255,.018); }
.auth-brand { display: flex; align-items: center; gap: 14px; letter-spacing: .14em; }
.auth-story h1 { max-width: 100%; font-family: var(--ion-font-family); font-size: clamp(34px, 10vw, 74px); line-height: 1; letter-spacing: -.045em; margin: 14px 0 20px; overflow-wrap: anywhere; }
.auth-story h1 em { color: #D8C5EC; font-weight: 700; font-style: normal; }
.auth-story .eyebrow { color: #D8C5EC; }
.auth-brand { align-items: flex-start; flex-direction: column; gap: 8px; }
.product-name { font-size: 12px; color: #CBD5E1; letter-spacing: .16em; text-transform: uppercase; }
.auth-story p { max-width: 600px; line-height: 1.7; color: #CBD5E1; margin: 0; overflow-wrap: anywhere; }
.auth-benefits { display: flex; gap: 20px; flex-wrap: wrap; color: #CBD5E1; font-size: 12px; }
.auth-benefits span { min-width: 0; display: flex; align-items: center; gap: 7px; }
.auth-benefits ion-icon { color: var(--mashal-on-dark); font-size: 18px; }
.auth-panel { min-width: 0; padding: 28px max(18px, env(safe-area-inset-right)) max(28px, env(safe-area-inset-bottom)) max(18px, env(safe-area-inset-left)); display: grid; place-items: center; }
.auth-card { width: min(100%, 430px); min-width: 0; border-radius: 26px; padding: 30px; background: var(--mashal-paper); border: 1px solid rgba(11,31,58,.08); box-shadow: var(--mashal-shadow); display: grid; gap: 18px; }
.auth-card h2 { max-width: 100%; font-family: var(--ion-font-family); font-size: clamp(26px, 8vw, 30px); line-height: 1.12; margin: 0; color: var(--mashal-navy); overflow-wrap: anywhere; }
.auth-card > p { color: var(--mashal-muted); margin: -8px 0 2px; line-height: 1.55; font-size: 13px; }
.google { justify-content: center; }
.google ion-icon { color: white; font-size: 20px; }
.setup-note { padding: 14px; border-radius: 14px; background: var(--mashal-warning-soft); display: grid; gap: 5px; }
.setup-note strong { font-size: 12px; }
.setup-note span, .auth-fineprint { color: var(--mashal-muted); font-size: 11px; line-height: 1.5; text-align: center; }
.setup-note code { background: rgba(255,255,255,.6); padding: 1px 4px; border-radius: 4px; }
.debug-actions { display: grid; gap: 8px; }
.debug-message { color: var(--mashal-danger); font-size: 11px; line-height: 1.4; text-align: center; }
.auth-card .button { height: auto; min-width: 0; padding-top: 12px; padding-bottom: 12px; white-space: normal; text-align: center; line-height: 1.35; }
@media (max-width: 359px) {
  .auth-story { min-height: auto; padding: max(24px, env(safe-area-inset-top)) 16px 24px; gap: 28px; }
  .auth-story h1 { font-size: 32px; }
  .auth-benefits { gap: 12px; }
  .auth-panel { padding: 18px 12px max(18px, env(safe-area-inset-bottom)); }
  .auth-card { padding: 21px 18px; border-radius: 20px; gap: 14px; }
}
@media (max-height: 600px) and (orientation: landscape) {
  .auth-story { min-height: auto; padding-top: 24px; padding-bottom: 24px; gap: 22px; }
  .auth-story h1 { font-size: clamp(32px, 6vw, 48px); margin: 10px 0 12px; }
  .auth-panel { align-items: start; padding-top: 20px; padding-bottom: 20px; }
}
@media (min-width: 900px) {
  .auth-page { grid-template-columns: minmax(0, 1.15fr) minmax(360px, .85fr); align-content: stretch; }
  .auth-story { min-height: 100%; padding: max(46px, env(safe-area-inset-top)) 60px max(46px, env(safe-area-inset-bottom)); }
  .auth-panel { padding: 50px; }
}
</style>
