import { expect, test as base } from '@playwright/test'

/** Every test also fails on an uncaught page error or a hydration mismatch, whatever it checks. */
export const test = base.extend<{ pageHealth: void }>({
  pageHealth: [
    async ({ page }, use) => {
      const problems: string[] = []

      page.on('pageerror', (error) => problems.push(`Uncaught: ${error.message}`))
      page.on('console', (message) => {
        if (/hydrat/i.test(message.text())) {
          problems.push(`Console: ${message.text()}`)
        }
      })

      await use()

      expect(problems).toEqual([])
    },
    { auto: true },
  ],
})

export { expect }
