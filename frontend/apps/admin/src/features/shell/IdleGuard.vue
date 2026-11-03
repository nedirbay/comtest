<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { AppDialog } from '@comtest/ui'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { toastWarning } from '@/shared/toast'

const props = withDefaults(defineProps<{ idleMinutes?: number; warnSeconds?: number }>(), {
  idleMinutes: 30,
  warnSeconds: 60,
})

const ACTIVITY_EVENTS = ['pointerdown', 'pointermove', 'keydown', 'wheel', 'touchstart'] as const
const CHANNEL = 'comtest-activity'
const TICK_MS = 1000
/** Moving the mouse fires very often, and the clock needs no better than this. */
const ACTIVITY_THROTTLE_MS = 5000
/** How long the note on the sign-in page stays. It explains why the user ended up there. */
const SIGNED_OUT_NOTE_MS = 15_000

const { t } = useI18n({ useScope: 'global' })
const auth = useAuthStore()

const limitMs = computed(() => props.idleMinutes * 60_000)
const warnMs = computed(() => props.warnSeconds * 1000)

const lastActivity = ref(Date.now())
const now = ref(Date.now())
const warning = ref(false)
let timer: ReturnType<typeof setInterval> | undefined
let channel: BroadcastChannel | null = null
let signingOut = false

const secondsLeft = computed(() =>
  Math.max(0, Math.ceil((lastActivity.value + limitMs.value - now.value) / 1000)),
)

function markActive(): void {
  lastActivity.value = Date.now()
  warning.value = false
}

function onActivity(): void {
  // While the warning is up, only the button counts: a brushed mouse must not hide the question.
  if (warning.value || Date.now() - lastActivity.value < ACTIVITY_THROTTLE_MS) {
    return
  }

  markActive()
  channel?.postMessage('active')
}

function stayActive(): void {
  markActive()
  channel?.postMessage('active')
}

async function signOut(): Promise<void> {
  if (signingOut) {
    return
  }

  signingOut = true
  warning.value = false
  await auth.logout()
  toastWarning(t('admin.idle.signedOut'), SIGNED_OUT_NOTE_MS)
}

// Comparing clocks, rather than counting ticks, also catches a laptop that slept through the limit.
function check(): void {
  now.value = Date.now()

  const idleFor = now.value - lastActivity.value

  if (idleFor >= limitMs.value) {
    void signOut()
  } else if (idleFor >= limitMs.value - warnMs.value) {
    warning.value = true
  }
}

onMounted(() => {
  for (const name of ACTIVITY_EVENTS) {
    window.addEventListener(name, onActivity, { passive: true })
  }

  document.addEventListener('visibilitychange', check)

  // Work in one tab keeps the other tabs of the same session alive.
  if (typeof BroadcastChannel !== 'undefined') {
    channel = new BroadcastChannel(CHANNEL)
    channel.onmessage = () => markActive()
  }

  timer = setInterval(check, TICK_MS)
})

onBeforeUnmount(() => {
  for (const name of ACTIVITY_EVENTS) {
    window.removeEventListener(name, onActivity)
  }

  document.removeEventListener('visibilitychange', check)
  channel?.close()
  clearInterval(timer)
})
</script>

<template>
  <AppDialog
    :model-value="warning"
    :title="t('admin.idle.title')"
    @update:model-value="(isOpen) => !isOpen && stayActive()"
  >
    <p class="text-sm text-ink" data-testid="idle-message">
      {{ t('admin.idle.message', { seconds: secondsLeft }) }}
    </p>
    <template #actions>
      <el-button @click="signOut">{{ t('auth.logout') }}</el-button>
      <el-button type="primary" data-testid="idle-stay" @click="stayActive">
        {{ t('admin.idle.stay') }}
      </el-button>
    </template>
  </AppDialog>
</template>
