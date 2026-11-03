import { expect, test as base } from '@playwright/test'

/** Every test also fails on an uncaught page error, hydration mismatch or CSP violation. */
export const test = base.extend<{ pageHealth: void; expectedProblems: RegExp | null }>({
  /** For a test that provokes a violation on purpose, to show that it gets blocked. */
  expectedProblems: [null, { option: true }],
  pageHealth: [
    async ({ page, expectedProblems }, use) => {
      const problems: string[] = []

      page.on('pageerror', (error) => problems.push(`Uncaught: ${error.message}`))
      page.on('console', (message) => {
        const text = message.text()

        if (/hydrat|content security policy/i.test(text) && !expectedProblems?.test(text)) {
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
