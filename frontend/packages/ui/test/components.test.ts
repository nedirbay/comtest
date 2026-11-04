import { ApiError } from '@comtest/api-client'
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { createI18n } from 'vue-i18n'
import AppTable from '../src/components/AppTable.vue'
import EmptyState from '../src/components/EmptyState.vue'
import ErrorState from '../src/components/ErrorState.vue'
import SafeHtml from '../src/components/SafeHtml.vue'
import StatusTag from '../src/components/StatusTag.vue'
import { uiMessages } from '../src/i18n'

const i18n = createI18n({ legacy: false, locale: 'tk', fallbackLocale: 'en', messages: uiMessages })
const global = {
  plugins: [i18n],
  // The apps resolve Element Plus by a build plugin, which tests do not run.
  stubs: {
    'el-button': { template: '<button><slot /></button>' },
    'el-table': true,
    'el-table-column': true,
    'el-pagination': true,
    'el-tag': { template: '<span class="tag"><slot /></span>' },
  },
}

describe('StatusTag', () => {
  it('translates a status', () => {
    const tag = mount(StatusTag, { props: { kind: 'orderStatus', value: 'pending' }, global })

    expect(tag.text()).toBe('Garaşylýar')
  })

  it('shows a status it does not know as it came, rather than a translation key', () => {
    const tag = mount(StatusTag, { props: { kind: 'orderStatus', value: 'OnTheMoon' }, global })

    expect(tag.text()).toBe('OnTheMoon')
  })
})

describe('ErrorState', () => {
  it('prefers the server text and shows the reference code', () => {
    const error = new ApiError({
      status: 500,
      code: 'x',
      title: 'Serwer ýalňyşlygy',
      correlationId: 'abc123',
    })
    const state = mount(ErrorState, { props: { error }, global })

    expect(state.text()).toContain('Serwer ýalňyşlygy')
    expect(state.text()).toContain('abc123')
    expect(state.attributes('role')).toBe('alert')
  })

  it('explains a lost connection in plain words', () => {
    const state = mount(ErrorState, { props: { error: ApiError.network(new TypeError()) }, global })

    expect(state.text()).toContain('Internetiňizi')
  })

  it('offers a retry only when asked to, and reports the click', async () => {
    expect(mount(ErrorState, { global }).find('button').exists()).toBe(false)

    const state = mount(ErrorState, { props: { retryable: true }, global })

    await state.find('button').trigger('click')
    expect(state.emitted('retry')).toHaveLength(1)
  })
})

describe('AppTable', () => {
  const columns = [{ key: 'name', label: 'Ady' }]

  it('shows an empty state instead of an empty table', () => {
    const table = mount(AppTable, { props: { columns, rows: [] }, global })

    expect(table.text()).toContain('Bu ýerde entek hiç zat ýok')
  })

  it('shows the failure and lets the user retry', async () => {
    const table = mount(AppTable, {
      props: {
        columns,
        rows: [],
        error: new ApiError({ status: 500, code: 'x', title: 'Ýalňyşlyk' }),
      },
      global,
    })

    expect(table.text()).toContain('Ýalňyşlyk')
    await table.find('button').trigger('click')
    expect(table.emitted('retry')).toHaveLength(1)
  })

  it('shows a loading placeholder before the first rows arrive', () => {
    const table = mount(AppTable, { props: { columns, rows: [], loading: true }, global })

    expect(table.find('[role="status"]').exists()).toBe(true)
  })
})

describe('EmptyState', () => {
  it('has a default title in the current language', () => {
    expect(mount(EmptyState, { global }).text()).toContain('Bu ýerde entek hiç zat ýok')
  })
})

describe('SafeHtml', () => {
  it('renders formatting and drops anything that could run', () => {
    const html = mount(SafeHtml, {
      props: {
        html: '<p><strong>ok</strong></p><img src="x" onerror="alert(1)"><script>alert(2)</script>',
      },
    }).html()

    expect(html).toContain('<strong>ok</strong>')
    expect(html).not.toContain('onerror')
    expect(html).not.toContain('<script')
  })
})
