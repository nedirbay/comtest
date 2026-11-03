import type { Plugin } from 'vite'

export const CSS_LAYER_ORDER: string
export function elementPlusCssLayer(layer?: string): Plugin
export function adminContentSecurityPolicy(options: {
  apiUrl?: string
  extraOrigins?: string
}): Plugin
