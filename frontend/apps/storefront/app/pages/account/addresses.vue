<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import {
  AppButton,
  AppDialog,
  AppForm,
  EmptyState,
  ErrorState,
  SkeletonBlock,
  formatPhone,
  useApiErrorMessage,
  useForm,
} from '@comtest/ui'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

definePageMeta({ middleware: 'auth', requiresAuth: true })

type Address = Schema<'AddressDto'>

const { t } = useI18n()
const api = useApi()
const queryClient = useQueryClient()
const describeError = useApiErrorMessage()
const { regionLabel } = useRegions()

useHead({ title: computed(() => t('store.account.addresses')) })

const QUERY_KEY = ['account', 'addresses']

const addresses = useQuery({
  queryKey: QUERY_KEY,
  queryFn: () => unwrap(api.client.GET('/api/v1/account/addresses')),
})

const refresh = () => queryClient.invalidateQueries({ queryKey: QUERY_KEY })

const editing = ref<Address | 'new' | null>(null)
const label = ref('')
const isDefault = ref(false)

const dialogOpen = computed({
  get: () => editing.value !== null,
  set: (open) => {
    if (!open) {
      editing.value = null
    }
  },
})

const form = useForm({
  schema: addressSchema,
  initial: { ...EMPTY_ADDRESS },
  translate: (key) => t(key),
  describeError,
  submit: async (values) => {
    const body = {
      address: toAddressData(values),
      label: label.value.trim() || null,
      isDefault: isDefault.value,
    }
    const target = editing.value

    await (target && target !== 'new'
      ? unwrap(
          api.client.PUT('/api/v1/account/addresses/{id}', {
            params: { path: { id: target.id } },
            body,
          }),
        )
      : unwrap(api.client.POST('/api/v1/account/addresses', { body })))

    editing.value = null
    await refresh()
  },
})

function open(target: Address | 'new'): void {
  form.reset()
  label.value = ''
  isDefault.value = target === 'new' && !addresses.data.value?.length

  if (target !== 'new') {
    const { address } = target

    Object.assign(form.values, {
      ...address,
      district: address.district ?? '',
      city: address.city ?? '',
      line2: address.line2 ?? '',
      postalCode: address.postalCode ?? '',
    })
    label.value = target.label ?? ''
    isDefault.value = target.isDefault
  }

  editing.value = target
}

const deleting = ref<Address | null>(null)
const deleteOpen = computed({
  get: () => deleting.value !== null,
  set: (value) => {
    if (!value) {
      deleting.value = null
    }
  },
})

const remove = useMutation({
  mutationFn: (id: string) =>
    unwrap(api.client.DELETE('/api/v1/account/addresses/{id}', { params: { path: { id } } })),
  onSuccess: async () => {
    deleting.value = null
    await refresh()
  },
})
</script>

<template>
  <AccountShell :title="t('store.account.addresses')">
    <ErrorState
      v-if="addresses.isError.value"
      :error="addresses.error.value"
      retryable
      @retry="addresses.refetch()"
    />
    <SkeletonBlock v-else-if="addresses.isPending.value" :lines="4" class="max-w-xl" />
    <template v-else>
      <EmptyState
        v-if="!addresses.data.value?.length"
        :title="t('store.addresses.emptyTitle')"
        :description="t('store.addresses.emptyText')"
      />
      <ul v-else class="grid max-w-3xl gap-3 sm:grid-cols-2">
        <li
          v-for="item in addresses.data.value"
          :key="item.id"
          class="flex flex-col rounded-card border border-line bg-surface p-4"
          data-testid="address-card"
        >
          <p class="flex flex-wrap items-center gap-2 font-semibold text-ink">
            {{ item.label || item.address.recipient }}
            <el-tag v-if="item.isDefault" type="success" size="small" disable-transitions>
              {{ t('store.addresses.default') }}
            </el-tag>
          </p>
          <p class="mt-1 flex-1 text-sm text-ink-muted">
            {{ item.address.recipient }}, {{ formatPhone(item.address.phone) }}
            <br />
            {{ regionLabel(item.address.region) }}
            <template v-if="item.address.city">, {{ item.address.city }}</template>
            , {{ item.address.line1 }}
          </p>
          <div class="mt-3 flex gap-2">
            <AppButton size="small" data-testid="edit-address" @click="open(item)">
              {{ t('store.addresses.edit') }}
            </AppButton>
            <AppButton
              size="small"
              variant="danger"
              plain
              data-testid="delete-address"
              @click="deleting = item"
            >
              {{ t('common.delete') }}
            </AppButton>
          </div>
        </li>
      </ul>
      <AppButton variant="primary" class="mt-4" data-testid="add-address" @click="open('new')">
        {{ t('store.addresses.add') }}
      </AppButton>
    </template>

    <AppDialog
      v-model="dialogOpen"
      :title="editing === 'new' ? t('store.addresses.add') : t('store.addresses.edit')"
      width="min(40rem, calc(100vw - 2rem))"
      :confirm-text="t('common.save')"
      :loading="form.submitting.value"
      @confirm="form.handleSubmit"
    >
      <AppForm :error="form.formError.value" @submit="form.handleSubmit">
        <AddressFields v-model="form.values" />
        <el-form-item :label="t('store.addresses.label')">
          <el-input
            v-model="label"
            :placeholder="t('store.addresses.labelPlaceholder')"
            :maxlength="100"
          />
        </el-form-item>
        <el-checkbox v-model="isDefault">{{ t('store.addresses.makeDefault') }}</el-checkbox>
      </AppForm>
    </AppDialog>

    <AppDialog
      v-model="deleteOpen"
      :title="t('store.addresses.deleteTitle')"
      confirm-variant="danger"
      :confirm-text="t('common.delete')"
      :loading="remove.isPending.value"
      @confirm="deleting && remove.mutate(deleting.id)"
    >
      <p class="text-sm text-ink-muted">{{ t('store.addresses.deleteText') }}</p>
      <p v-if="remove.isError.value" class="mt-2 text-sm text-danger" role="alert">
        {{ describeError(remove.error.value) }}
      </p>
    </AppDialog>
  </AccountShell>
</template>
