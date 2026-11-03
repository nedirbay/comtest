<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { defineAsyncComponent } from 'vue'
import { RouterView, useRoute } from 'vue-router'

const layouts = {
  auth: defineAsyncComponent(() => import('./AuthLayout.vue')),
  admin: defineAsyncComponent(() => import('./AdminLayout.vue')),
}

const route = useRoute()
const auth = useAuthStore()
</script>

<template>
  <!-- Everything inside belongs to one store. A switch starts over, with no old data on screen. -->
  <component
    :is="route.meta.layout ? layouts[route.meta.layout] : RouterView"
    :key="route.meta.layout === 'admin' ? (auth.claims?.tenantId ?? 'platform') : 'other'"
  />
</template>
