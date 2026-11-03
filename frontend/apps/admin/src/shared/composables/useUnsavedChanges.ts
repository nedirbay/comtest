import { useAuthStore } from '@comtest/auth'
import { ElMessageBox } from 'element-plus'
import 'element-plus/es/components/message-box/style/css'
import { onBeforeUnmount, onMounted, toValue, type MaybeRefOrGetter } from 'vue'
import { useI18n } from 'vue-i18n'
import { onBeforeRouteLeave } from 'vue-router'

/**
 * Warns before losing edits: a dialog for moves inside the panel, the browser's own question for
 * closing the tab. A session that has ended takes the user away regardless, as the data can no
 * longer be saved and must not stay on the screen.
 */
export function useUnsavedChanges(isDirty: MaybeRefOrGetter<boolean>) {
  const { t } = useI18n({ useScope: 'global' })
  const auth = useAuthStore()

  function onBeforeUnload(event: BeforeUnloadEvent): void {
    if (toValue(isDirty)) {
      event.preventDefault()
    }
  }

  onMounted(() => window.addEventListener('beforeunload', onBeforeUnload))
  onBeforeUnmount(() => window.removeEventListener('beforeunload', onBeforeUnload))

  async function confirmLeave(): Promise<boolean> {
    if (!toValue(isDirty) || !auth.isAuthenticated) {
      return true
    }

    try {
      await ElMessageBox.confirm(t('admin.unsaved.message'), t('admin.unsaved.title'), {
        confirmButtonText: t('admin.unsaved.leave'),
        cancelButtonText: t('admin.unsaved.stay'),
        type: 'warning',
        closeOnPressEscape: true,
      })

      return true
    } catch {
      return false
    }
  }

  onBeforeRouteLeave(() => confirmLeave())

  return { confirmLeave }
}
