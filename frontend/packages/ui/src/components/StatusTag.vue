<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { normalizeStatus, statusTagType, type StatusKind } from '../domain/status'

const props = defineProps<{ kind: StatusKind; value: string }>()

const { t, te } = useI18n({ useScope: 'global' })

const key = computed(() => `enums.${props.kind}.${normalizeStatus(props.value)}`)
</script>

<template>
  <el-tag :type="statusTagType(value)" disable-transitions>
    {{ te(key) ? t(key) : value }}
  </el-tag>
</template>
