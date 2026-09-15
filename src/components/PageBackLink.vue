<script setup lang="ts">
import { IonIcon } from '@ionic/vue'
import { arrowBackOutline } from 'ionicons/icons'

defineProps<{
  to: string
  text: string
}>()
</script>

<template>
  <router-link class="page-back-link" :to="to">
    <ion-icon :icon="arrowBackOutline" aria-hidden="true" />
    <span>{{ text }}</span>
  </router-link>
</template>
