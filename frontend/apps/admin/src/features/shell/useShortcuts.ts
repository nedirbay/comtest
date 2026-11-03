import { useAuthStore } from '@comtest/auth'
import { onBeforeUnmount, onMounted, type Ref } from 'vue'
import { useRouter } from 'vue-router'
import { NAV_SECTIONS } from '@/app/navigation'

const SEQUENCE_TIMEOUT_MS = 1200

const GO_TO: Record<string, string> = {
  d: 'dashboard',
  o: 'orders',
  p: 'products',
  c: 'customers',
  s: 'stock',
}

/** Single letters must not fire while the user is typing into something. */
function isTyping(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false
  }

  return (
    target.isContentEditable ||
    ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName) ||
    target.closest('[contenteditable="true"]') !== null
  )
}

export function useShortcuts(paletteOpen: Ref<boolean>, helpOpen: Ref<boolean>): void {
  const router = useRouter()
  const auth = useAuthStore()
  let goPending = false
  let goTimer: ReturnType<typeof setTimeout> | undefined

  function goTo(name: string): void {
    const item = NAV_SECTIONS.flatMap((section) => section.items).find(
      (candidate) => candidate.name === name,
    )

    if (item && (!item.permission || auth.can(item.permission))) {
      void router.push({ name })
    }
  }

  function onKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault()
      paletteOpen.value = !paletteOpen.value

      return
    }

    if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)) {
      return
    }

    if (goPending) {
      goPending = false
      clearTimeout(goTimer)

      const name = GO_TO[event.key]

      if (name) {
        event.preventDefault()
        goTo(name)
      }

      return
    }

    if (event.key === '/') {
      event.preventDefault()
      paletteOpen.value = true
    } else if (event.key === '?') {
      event.preventDefault()
      helpOpen.value = true
    } else if (event.key === 'g') {
      goPending = true
      goTimer = setTimeout(() => {
        goPending = false
      }, SEQUENCE_TIMEOUT_MS)
    }
  }

  onMounted(() => window.addEventListener('keydown', onKeydown))
  onBeforeUnmount(() => {
    window.removeEventListener('keydown', onKeydown)
    clearTimeout(goTimer)
  })
}
