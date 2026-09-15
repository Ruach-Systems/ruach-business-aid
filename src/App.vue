<script setup lang="ts">
import BrandLogo from '@/components/BrandLogo.vue'
import { IonApp } from '@ionic/vue'
import { watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useBusinessStore } from '@/stores/business'
import { resolveAppRoute } from '@/router/routeDecision'

const store = useBusinessStore()
const route = useRoute()
const router = useRouter()

watch(
  () => [store.authReady, store.dataReady, store.user, store.business, route.path] as const,
  ([authReady, dataReady, user, business, path]) => {
    const destination = resolveAppRoute(authReady, dataReady, Boolean(user), Boolean(business), path)
    if (destination) void router.replace(destination)
  },
  { immediate: true }
)
</script>

<template>
  <ion-app>
    <div v-if="!store.authReady || !store.dataReady" class="app-loading">
      <BrandLogo reversed />
      <strong>Business Aid</strong>
      <span>{{ store.user ? 'Loading your business…' : 'Preparing your business…' }}</span>
    </div>
    <router-view v-else />
  </ion-app>
</template>
