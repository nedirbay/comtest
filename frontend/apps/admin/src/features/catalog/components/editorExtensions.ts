import Link from '@tiptap/extension-link'
import StarterKit from '@tiptap/starter-kit'

/**
 * What the editor can produce. It is narrower than the list in `sanitize.ts`, the same one the API
 * keeps, so anything typed or pasted here survives saving exactly as it was. Whatever is not
 * listed cannot enter the document at all, a pasted `<script>` or `style` included.
 */
export function editorExtensions() {
  return [
    StarterKit.configure({
      code: false,
      codeBlock: false,
      horizontalRule: false,
      strike: false,
      link: false,
      heading: { levels: [2, 3, 4] },
    }),
    Link.configure({
      openOnClick: false,
      autolink: true,
      protocols: ['http', 'https', 'mailto', 'tel'],
      HTMLAttributes: { rel: 'noopener noreferrer', target: '_blank' },
      isAllowedUri: (url) => /^(https?:|mailto:|tel:)/i.test(url),
    }),
  ]
}

/**
 * The API also allows tables and images in a description, for text saved by other means. The editor
 * has no such blocks, and would drop them from the text on the first edit, so the user must know.
 */
export function hasUnsupportedContent(html: string): boolean {
  return /<(table|img)\b/i.test(html)
}
