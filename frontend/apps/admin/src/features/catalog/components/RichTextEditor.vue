<script setup lang="ts">
import { sanitizeHtml, safeUrl } from '@comtest/ui'
import { Link as LinkIcon } from '@element-plus/icons-vue'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { ElMessageBox } from 'element-plus'
import 'element-plus/es/components/message-box/style/css'
import { computed, onBeforeUnmount, watch, type Component } from 'vue'
import { useI18n } from 'vue-i18n'
import { editorExtensions, hasUnsupportedContent } from './editorExtensions'

const props = defineProps<{ disabled?: boolean; label?: string }>()
const model = defineModel<string>({ required: true })

const { t } = useI18n({ useScope: 'global' })

const EMPTY_DOCUMENT = '<p></p>'

/** What the editor last reported. Its own text coming back must not restart it, or the cursor jumps. */
let lastEmitted = model.value

const editor = useEditor({
  content: sanitizeHtml(model.value),
  editable: !props.disabled,
  // It would add a <style> element to the page, which a content security policy refuses. What it
  // holds that this editor needs is in the styles below.
  injectCSS: false,
  extensions: editorExtensions(),
  editorProps: {
    attributes: {
      class: 'editor-content',
      role: 'textbox',
      'aria-multiline': 'true',
      'aria-label': props.label ?? '',
      'data-testid': 'rich-text',
    },
  },
  onUpdate: ({ editor: current }) => {
    const html = current.getHTML()

    lastEmitted = html === EMPTY_DOCUMENT ? '' : sanitizeHtml(html)
    model.value = lastEmitted
  },
})

// The form loads its values after this component has mounted, and a reload replaces them.
watch(model, (next) => {
  const current = editor.value

  if (!current) {
    return
  }

  if (next !== lastEmitted) {
    lastEmitted = next
    current.commands.setContent(sanitizeHtml(next), { emitUpdate: false })
  }
})

watch(
  () => props.disabled,
  (disabled) => editor.value?.setEditable(!disabled),
)

onBeforeUnmount(() => editor.value?.destroy())

const willLoseContent = computed(() => hasUnsupportedContent(model.value))

interface ToolButton {
  key: string
  label: string
  text: string
  icon?: Component
  active: () => boolean
  run: () => void
}

function chain() {
  // Only called from the toolbar, which is drawn once the editor exists.
  return editor.value!.chain().focus()
}

const headingLevels = [2, 3, 4] as const

// The toolbar's buttons hold on to no focus when clicked (`mousedown.prevent` in the template), so
// the text keeps its cursor and selection. Otherwise fast typing right after a click lands on the
// button, where a space would press it again.

const buttons = computed<ToolButton[]>(() => {
  const current = editor.value

  if (!current) {
    return []
  }

  return [
    {
      key: 'bold',
      label: t('admin.editor.bold'),
      text: 'B',
      active: () => current.isActive('bold'),
      run: () => chain().toggleBold().run(),
    },
    {
      key: 'italic',
      label: t('admin.editor.italic'),
      text: 'I',
      active: () => current.isActive('italic'),
      run: () => chain().toggleItalic().run(),
    },
    {
      key: 'underline',
      label: t('admin.editor.underline'),
      text: 'U',
      active: () => current.isActive('underline'),
      run: () => chain().toggleUnderline().run(),
    },
    ...headingLevels.map((level) => ({
      key: `h${level}`,
      label: t('admin.editor.heading', { level }),
      text: `H${level}`,
      active: () => current.isActive('heading', { level }),
      run: () => chain().toggleHeading({ level }).run(),
    })),
    {
      key: 'bullets',
      label: t('admin.editor.bulletList'),
      text: '•',
      active: () => current.isActive('bulletList'),
      run: () => chain().toggleBulletList().run(),
    },
    {
      key: 'numbers',
      label: t('admin.editor.orderedList'),
      text: '1.',
      active: () => current.isActive('orderedList'),
      run: () => chain().toggleOrderedList().run(),
    },
    {
      key: 'quote',
      label: t('admin.editor.quote'),
      text: '❝',
      active: () => current.isActive('blockquote'),
      run: () => chain().toggleBlockquote().run(),
    },
    {
      key: 'link',
      label: t('admin.editor.link'),
      text: '',
      icon: LinkIcon,
      active: () => current.isActive('link'),
      run: () => void askForLink(),
    },
  ]
})

async function askForLink(): Promise<void> {
  const current = editor.value

  if (!current) {
    return
  }

  try {
    const { value } = (await ElMessageBox.prompt(
      t('admin.editor.linkPrompt'),
      t('admin.editor.link'),
      {
        inputValue: current.getAttributes('link').href ?? '',
        inputPattern: /^$|^(https?:\/\/|mailto:|tel:)/,
        inputErrorMessage: t('admin.validation.url'),
        confirmButtonText: t('common.confirm'),
        cancelButtonText: t('common.cancel'),
      },
    )) as { value: string }

    const url = safeUrl(value)

    if (url) {
      chain().extendMarkRange('link').setLink({ href: url }).run()
    } else {
      chain().extendMarkRange('link').unsetLink().run()
    }
  } catch {
    // Cancelled, which changes nothing.
  }
}
</script>

<template>
  <div class="w-full overflow-hidden rounded-control border border-line-strong bg-surface">
    <div
      v-if="!disabled"
      class="flex flex-wrap gap-0.5 border-b border-line p-1"
      role="toolbar"
      :aria-label="t('admin.editor.toolbar')"
    >
      <button
        v-for="button in buttons"
        :key="button.key"
        type="button"
        class="inline-flex h-8 min-w-8 items-center justify-center rounded px-1.5 text-sm font-bold text-ink-muted hover:bg-primary-soft hover:text-ink"
        :class="{ '!bg-primary-soft !text-primary': button.active() }"
        :aria-label="button.label"
        :aria-pressed="button.active()"
        :title="button.label"
        :data-testid="`editor-${button.key}`"
        @mousedown.prevent
        @click="button.run()"
      >
        <el-icon v-if="button.icon"><component :is="button.icon" /></el-icon>
        <template v-else>{{ button.text }}</template>
      </button>
    </div>
    <EditorContent :editor="editor" />
    <p v-if="willLoseContent" class="border-t border-line p-2 text-xs text-warning">
      {{ t('admin.editor.unsupported') }}
    </p>
  </div>
</template>

<style>
.ProseMirror {
  position: relative;
  overflow-wrap: break-word;
  white-space: pre-wrap;
  white-space: break-spaces;
  font-variant-ligatures: none;
}

.ProseMirror-hideselection *::selection {
  background: transparent;
}

.ProseMirror-separator {
  display: inline;
  width: 0;
  height: 0;
}

.editor-content {
  min-height: 11rem;
  max-height: 28rem;
  overflow-y: auto;
  padding: 0.75rem;
  line-height: 1.6;
  color: var(--theme-ink);
  outline: none;
}

.editor-content:focus-visible {
  box-shadow: inset 0 0 0 2px var(--theme-primary);
}

.editor-content > * + * {
  margin-top: 0.75em;
}

.editor-content h2,
.editor-content h3,
.editor-content h4 {
  font-weight: 700;
}

.editor-content h2 {
  font-size: 1.25rem;
}

.editor-content h3 {
  font-size: 1.125rem;
}

.editor-content ul {
  list-style: disc;
  padding-left: 1.25rem;
}

.editor-content ol {
  list-style: decimal;
  padding-left: 1.25rem;
}

.editor-content a {
  color: var(--theme-primary);
  text-decoration: underline;
}

.editor-content blockquote {
  border-left: 3px solid var(--theme-line-strong);
  padding-left: 1rem;
  color: var(--theme-ink-muted);
}
</style>
