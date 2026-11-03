import { setSanitizerWindow } from '@comtest/ui'

let ready: Promise<void> | undefined

/**
 * DOMPurify needs a DOM, which the server lacks. One window serves all requests: sanitizing keeps
 * no state in it. Until it exists `sanitizeHtml` returns nothing, so a failure hides text and never
 * lets unchecked HTML through.
 */
export default defineNuxtPlugin(async () => {
  ready ??= import('jsdom').then(({ JSDOM }) => {
    setSanitizerWindow(new JSDOM('').window as never)
  })

  await ready
})
