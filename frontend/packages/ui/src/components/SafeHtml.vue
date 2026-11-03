<script setup lang="ts">
import { computed } from 'vue'
import { sanitizeHtml } from '../security/sanitize'

const props = defineProps<{ html: string | null | undefined }>()

const clean = computed(() => sanitizeHtml(props.html))
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -- this is the one allowed place: DOMPurify runs above -->
  <div class="prose-content" v-html="clean" />
</template>

<style>
.prose-content {
  line-height: 1.65;
  color: var(--theme-ink);
}

.prose-content > * + * {
  margin-top: 0.75em;
}

.prose-content h2,
.prose-content h3,
.prose-content h4 {
  font-weight: 700;
  margin-top: 1.25em;
}

.prose-content h2 {
  font-size: 1.25rem;
}

.prose-content h3 {
  font-size: 1.125rem;
}

.prose-content ul {
  list-style: disc;
  padding-left: 1.25rem;
}

.prose-content ol {
  list-style: decimal;
  padding-left: 1.25rem;
}

.prose-content a {
  color: var(--theme-primary);
  text-decoration: underline;
}

.prose-content img {
  max-width: 100%;
  height: auto;
  border-radius: var(--radius-control);
}

.prose-content blockquote {
  border-left: 3px solid var(--theme-line-strong);
  padding-left: 1rem;
  color: var(--theme-ink-muted);
}

.prose-content table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.prose-content th,
.prose-content td {
  border: 1px solid var(--theme-line);
  padding: 0.5rem 0.75rem;
  text-align: left;
}
</style>
