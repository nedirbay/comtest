import type { Page } from '@playwright/test'
import { expect, test } from './support/fixtures'
import { ADMIN_URL, STOREFRONT_URL } from './support/stack'

async function styleOf(page: Page, selector: string, property: string): Promise<string> {
  return page
    .locator(selector)
    .first()
    .evaluate((element, name) => getComputedStyle(element).getPropertyValue(name), property)
}

/**
 * Tailwind's reset removes all margins, paddings and backgrounds. If it ever outranks Element Plus,
 * every test that only clicks and reads still passes on a visibly broken page. So look at the styles.
 */
for (const [app, url] of [
  ['storefront', `${STOREFRONT_URL}/login`],
  ['admin', `${ADMIN_URL}/tk/login`],
] as const) {
  test.describe(`${app} styles`, () => {
    test.beforeEach(async ({ page }) => {
      await page.goto(url)
      await page.getByRole('button', { name: 'Girmek', exact: true }).waitFor()
    })

    test('the cascade layers are declared before any stylesheet', async ({ page }) => {
      const firstLayerRule = await page.evaluate(() => {
        const first = [...document.styleSheets][0]

        return first?.cssRules[0]?.cssText ?? ''
      })

      expect(firstLayerRule).toContain('theme, base, element-plus')
    })

    test('the reset does not strip Element Plus components', async ({ page }) => {
      expect(parseFloat(await styleOf(page, '.el-form-item', 'margin-bottom'))).toBeGreaterThan(0)
      expect(parseFloat(await styleOf(page, '.el-input__wrapper', 'padding-left'))).toBeGreaterThan(
        0,
      )
    })

    test('the primary button has our brand colour and white text', async ({ page }) => {
      const button = '.el-button--primary'

      // --color-brand-700
      expect(await styleOf(page, button, 'background-color')).toBe('rgb(20, 91, 69)')
      expect(await styleOf(page, button, 'color')).toBe('rgb(255, 255, 255)')
    })

    test('a utility class can override a component', async ({ page }) => {
      const button = page.locator('.el-button--primary').first()
      const parent = button.locator('xpath=..')
      const [buttonWidth, parentWidth] = await Promise.all([
        button.evaluate((element) => element.getBoundingClientRect().width),
        parent.evaluate((element) => element.getBoundingClientRect().width),
      ])

      // The block button uses w-full, while Element Plus sizes buttons to their content.
      expect(buttonWidth).toBe(parentWidth)
    })

    test('text uses our font, which covers Turkmen letters', async ({ page }) => {
      expect(await styleOf(page, 'body', 'font-family')).toContain('Manrope')
      await page.evaluate(() => document.fonts.ready)

      const loaded = await page.evaluate(() =>
        document.fonts.check('16px "Manrope Variable"', 'ňýžşäöüç'),
      )

      expect(loaded).toBe(true)
    })

    test('dark mode changes the surfaces and keeps the button readable', async ({ page }) => {
      await page.getByRole('button', { name: 'Gara tema' }).click()
      await expect(page.locator('html')).toHaveClass(/dark/)
      await page.waitForTimeout(400)

      expect(await styleOf(page, 'html', 'background-color')).toBe('rgb(19, 18, 16)')
      // --color-brand-300 background with --color-brand-950 text.
      expect(await styleOf(page, '.el-button--primary', 'background-color')).toBe(
        'rgb(124, 197, 169)',
      )
      expect(await styleOf(page, '.el-button--primary', 'color')).toBe('rgb(7, 33, 26)')
    })

    test('nothing scrolls sideways', async ({ page }) => {
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      )

      expect(overflow).toBeLessThanOrEqual(0)
    })
  })
}
