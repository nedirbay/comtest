import type { Meta, StoryObj } from '@storybook/vue3-vite'
import { ApiError } from '@comtest/api-client'
import { ref } from 'vue'
import AppButton from '../components/AppButton.vue'
import AppDialog from '../components/AppDialog.vue'
import EmptyState from '../components/EmptyState.vue'
import ErrorState from '../components/ErrorState.vue'
import PageHeader from '../components/PageHeader.vue'
import SkeletonBlock from '../components/SkeletonBlock.vue'

const meta: Meta = { title: 'Elements/Feedback' }

export default meta

export const Empty: StoryObj = {
  render: () => ({
    components: { EmptyState, AppButton },
    template: `
      <EmptyState description="Ilkinji harydyňyzy goşuň.">
        <AppButton variant="primary">Haryt goş</AppButton>
      </EmptyState>
    `,
  }),
}

export const NetworkError: StoryObj = {
  render: () => ({
    components: { ErrorState },
    setup: () => ({ error: ApiError.network(new TypeError('offline')) }),
    template: '<ErrorState :error="error" retryable />',
  }),
}

export const ServerError: StoryObj = {
  render: () => ({
    components: { ErrorState },
    setup: () => ({
      error: new ApiError({ status: 500, code: 'common.unexpected', correlationId: 'a1b2c3d4' }),
    }),
    template: '<ErrorState :error="error" retryable />',
  }),
}

export const Skeleton: StoryObj = {
  render: () => ({
    components: { SkeletonBlock },
    template: '<SkeletonBlock class="max-w-md" :lines="5" />',
  }),
}

export const Header: StoryObj = {
  render: () => ({
    components: { PageHeader, AppButton },
    template: `
      <PageHeader title="Harytlar" description="Dükanyňyzdaky ähli harytlar.">
        <template #actions><AppButton variant="primary">Haryt goş</AppButton></template>
      </PageHeader>
    `,
  }),
}

export const Dialog: StoryObj = {
  render: () => ({
    components: { AppDialog, AppButton },
    setup: () => ({ open: ref(true) }),
    template: `
      <AppButton @click="open = true">Aç</AppButton>
      <AppDialog v-model="open" title="Harydy pozmak" confirm-variant="danger" confirm-text="Poz">
        <p class="text-sm text-ink-muted">Bu hereketi yzyna gaýtaryp bolmaýar.</p>
      </AppDialog>
    `,
  }),
}
