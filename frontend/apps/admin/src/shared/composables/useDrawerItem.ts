import { ref, shallowRef } from 'vue'

/**
 * State for a list page with a form in a drawer. The form component mounts when something is being
 * edited, and unmounts after the closing animation. `session` goes on the component as its `key`:
 * opening again while the last one is still sliding away must give a new form too, or the user
 * would be editing one record with the values of another.
 */
export function useDrawerItem<Item>() {
  const mounted = ref(false)
  const open = ref(false)
  const item = shallowRef<Item | null>(null)
  const session = ref(0)

  return {
    mounted,
    open,
    item,
    session,
    /** Pass the row to edit, or nothing for a new one. */
    show(next: Item | null = null): void {
      item.value = next
      session.value += 1
      mounted.value = true
      open.value = true
    },
    hide(): void {
      open.value = false
    },
    closed(): void {
      mounted.value = false
      item.value = null
    },
  }
}
