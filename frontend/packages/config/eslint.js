import js from '@eslint/js'
import prettier from 'eslint-config-prettier'
import vue from 'eslint-plugin-vue'
import globals from 'globals'
import tseslint from 'typescript-eslint'
import vueParser from 'vue-eslint-parser'

const STORAGE_MESSAGE =
  'Use the preferences from @comtest/ui. Tokens and personal data must not be stored in the browser.'

export default tseslint.config(
  {
    ignores: [
      '**/dist/**',
      '**/.nuxt/**',
      '**/.output/**',
      '**/coverage/**',
      '**/storybook-static/**',
      '**/src/generated/**',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...vue.configs['flat/recommended'],
  {
    files: ['**/*.vue'],
    languageOptions: {
      parser: vueParser,
      parserOptions: { parser: tseslint.parser, sourceType: 'module' },
    },
  },
  {
    languageOptions: { globals: { ...globals.browser, ...globals.node } },
    rules: {
      // TypeScript already reports undefined names, and it knows about Nuxt's auto-imports.
      'no-undef': 'off',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/consistent-type-imports': 'error',
      'vue/multi-word-component-names': 'off',
      // An optional prop in TypeScript already says what absent means.
      'vue/require-default-prop': 'off',
      'vue/block-order': ['error', { order: ['script', 'template', 'style'] }],
      // Raw HTML is an XSS risk. An exception must go through DOMPurify and say so.
      'vue/no-v-html': 'error',
      '@typescript-eslint/no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: 'zod',
              message:
                'Import z from @comtest/ui/zod, which keeps it within the content security policy.',
              allowTypeImports: true,
            },
          ],
        },
      ],
      'no-restricted-globals': [
        'error',
        { name: 'localStorage', message: STORAGE_MESSAGE },
        { name: 'sessionStorage', message: STORAGE_MESSAGE },
      ],
      'no-restricted-properties': [
        'error',
        ...['window', 'globalThis', 'self'].flatMap((object) => [
          { object, property: 'localStorage', message: STORAGE_MESSAGE },
          { object, property: 'sessionStorage', message: STORAGE_MESSAGE },
        ]),
      ],
    },
  },
  {
    files: ['**/validation/zod.ts'],
    rules: { '@typescript-eslint/no-restricted-imports': 'off' },
  },
  {
    // Tests are allowed to look into browser storage, to prove that no token ended up there.
    files: ['**/test/**', 'e2e/**'],
    rules: { 'no-restricted-globals': 'off', 'no-restricted-properties': 'off' },
  },
  prettier,
)
