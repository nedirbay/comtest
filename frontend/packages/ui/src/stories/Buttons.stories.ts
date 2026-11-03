import type { Meta, StoryObj } from '@storybook/vue3-vite'
import AppButton from '../components/AppButton.vue'

const meta: Meta<typeof AppButton> = {
  title: 'Elements/AppButton',
  component: AppButton,
  args: { variant: 'primary', loading: false, block: false },
  argTypes: {
    variant: { control: 'select', options: ['primary', 'secondary', 'danger', 'ghost'] },
  },
  render: (args) => ({
    components: { AppButton },
    setup: () => ({ args }),
    template: '<AppButton v-bind="args">Sebede goş</AppButton>',
  }),
}

export default meta

export const Primary: StoryObj<typeof AppButton> = {}
export const Secondary: StoryObj<typeof AppButton> = { args: { variant: 'secondary' } }
export const Danger: StoryObj<typeof AppButton> = { args: { variant: 'danger' } }
export const Ghost: StoryObj<typeof AppButton> = { args: { variant: 'ghost' } }
export const Loading: StoryObj<typeof AppButton> = { args: { loading: true } }
export const Block: StoryObj<typeof AppButton> = { args: { block: true } }
