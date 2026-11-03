import { effectScope, watchEffect, type Directive, type EffectScope } from 'vue'
import { useAuthStore } from './store'

type Permission = string | readonly string[]

const scopes = new WeakMap<HTMLElement, EffectScope>()

/**
 * Hides what the user has no permission for: `v-can="'catalog.write'"`, or an array for any of
 * several. This is only about a tidy interface. The server checks every request by itself.
 */
export const vCan: Directive<HTMLElement, Permission> = {
  mounted(el, binding) {
    const auth = useAuthStore()
    const scope = effectScope()

    scopes.set(el, scope)
    scope.run(() => {
      watchEffect(() => {
        el.style.display = auth.can(binding.value) ? '' : 'none'
      })
    })
  },
  unmounted(el) {
    scopes.get(el)?.stop()
    scopes.delete(el)
  },
  // Permissions are unknown during server rendering, so start hidden rather than flash the content.
  getSSRProps() {
    return { style: { display: 'none' } }
  },
}
