<script setup lang="ts">
import { defineAsyncComponent } from 'vue'
import { RouterView, useRoute } from 'vue-router'

const layouts = {
  auth: defineAsyncComponent(() => import('./AuthLayout.vue')),
  admin: defineAsyncComponent(() => import('./AdminLayout.vue')),
}

const route = useRoute()
</script>

<template>
  <component :is="route.meta.layout ? layouts[route.meta.layout] : RouterView" />
</template>
