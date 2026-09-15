<script setup lang="ts">
import assets from 'virtual:mashal-brand'
withDefaults(defineProps<{ reversed?: boolean; iconOnly?: boolean }>(), { reversed: false, iconOnly: false })
</script>

<template>
  <img
    class="brand-logo" :class="{ 'brand-logo-icon': iconOnly }"
    :src="iconOnly ? (reversed ? assets.reversedIcon : assets.icon) : (reversed ? assets.reversedLogo : assets.logo)"
    :width="iconOnly ? 360 : 950" :height="iconOnly ? 360 : 320"
    alt="Mashal Systems" decoding="async"
  />
</template>

<style scoped>
.brand-logo { display: block; width: 208px; max-width: 100%; height: auto; flex: none; }
.brand-logo-icon { width: 64px; }
</style>
