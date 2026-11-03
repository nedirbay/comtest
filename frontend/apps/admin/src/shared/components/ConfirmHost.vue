<script setup lang="ts">
import { AppDialog, useApiErrorMessage } from '@comtest/ui'
import { computed, ref, shallowRef, watch } from 'vue'
import { useConfirm } from '../composables/useConfirm'

const confirm = useConfirm()
const describeError = useApiErrorMessage()
const running = ref(false)
const error = ref<string | null>(null)

/** What the dialog shows. It outlives the request, as the text must not vanish during the fade. */
const shown = shallowRef(confirm.request)

watch(
  () => confirm.request,
  (next) => {
    error.value = null

    if (next) {
      shown.value = next
    }
  },
)

const open = computed<boolean>({
  get: () => confirm.request !== null,
  set: (value) => {
    if (!value && !running.value) {
      confirm.close()
    }
  },
})

async function run(): Promise<void> {
  const request = confirm.request

  if (!request || running.value) {
    return
  }

  running.value = true
  error.value = null

  try {
    await request.action()
    confirm.close()
  } catch (cause) {
    error.value = describeError(cause)
  } finally {
    running.value = false
  }
}
</script>

<template>
  <AppDialog
    v-model="open"
    :title="shown?.title ?? ''"
    :confirm-text="shown?.confirmText"
    :confirm-variant="shown?.danger ? 'danger' : 'primary'"
    :loading="running"
    @confirm="run"
  >
    <p class="text-sm text-ink" data-testid="confirm-message">{{ shown?.message }}</p>
    <p v-if="error" class="mt-3 text-sm text-danger" role="alert">{{ error }}</p>
  </AppDialog>
</template>
