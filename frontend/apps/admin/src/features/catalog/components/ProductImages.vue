<script setup lang="ts">
import { AppButton, safeUrl } from '@comtest/ui'
import { ArrowLeft, ArrowRight, Delete, UploadFilled } from '@element-plus/icons-vue'
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ImageValues } from '../productForm'
import { ALLOWED_IMAGE_TYPES, useImageUpload } from './useImageUpload'

const props = defineProps<{ disabled?: boolean }>()
const images = defineModel<ImageValues[]>({ required: true })
const busy = defineModel<boolean>('busy', { default: false })

const { t } = useI18n({ useScope: 'global' })
const fileInput = ref<HTMLInputElement | null>(null)
const dropping = ref(false)
const draggedIndex = ref<number | null>(null)

const {
  uploads,
  upload,
  dismiss,
  busy: uploading,
} = useImageUpload((asset) => {
  images.value = [
    ...images.value,
    {
      mediaAssetId: asset.id,
      altText: '',
      thumbnail: asset.thumbnail,
      medium: asset.medium,
    },
  ]
})

watch(uploading, (value) => {
  busy.value = value
})

function addFiles(files: FileList | null | undefined): void {
  for (const file of Array.from(files ?? [])) {
    void upload(file)
  }
}

function onFilesChosen(event: Event): void {
  const input = event.target as HTMLInputElement

  addFiles(input.files)
  // Choosing the same file again after a failure must fire another change event.
  input.value = ''
}

function onDrop(event: DragEvent): void {
  dropping.value = false

  if (!props.disabled) {
    addFiles(event.dataTransfer?.files)
  }
}

function move(from: number, to: number): void {
  if (to < 0 || to >= images.value.length || from === to) {
    return
  }

  const next = [...images.value]
  const [item] = next.splice(from, 1)

  if (item) {
    next.splice(to, 0, item)
    images.value = next
  }
}

function removeImage(index: number): void {
  images.value = images.value.filter((_, position) => position !== index)
}

function onThumbDrop(index: number): void {
  if (draggedIndex.value !== null) {
    move(draggedIndex.value, index)
  }

  draggedIndex.value = null
}
</script>

<template>
  <div class="space-y-4">
    <div
      v-if="!disabled"
      class="flex flex-col items-center gap-2 rounded-card border-2 border-dashed p-6 text-center transition-colors"
      :class="dropping ? 'border-primary bg-primary-soft' : 'border-line-strong'"
      data-testid="image-dropzone"
      @dragover.prevent="dropping = true"
      @dragleave.prevent="dropping = false"
      @drop.prevent="onDrop"
    >
      <el-icon :size="32" class="text-ink-subtle"><UploadFilled /></el-icon>
      <p class="text-sm font-semibold text-ink">{{ t('admin.upload.dropHere') }}</p>
      <p class="text-xs text-ink-subtle">{{ t('admin.upload.limits') }}</p>
      <AppButton @click="fileInput?.click()">{{ t('admin.upload.choose') }}</AppButton>
      <input
        ref="fileInput"
        type="file"
        class="sr-only"
        multiple
        :accept="ALLOWED_IMAGE_TYPES.join(',')"
        tabindex="-1"
        :aria-label="t('admin.upload.choose')"
        data-testid="image-input"
        @change="onFilesChosen"
      />
    </div>

    <ul v-if="uploads.length" class="space-y-2" data-testid="upload-list">
      <li
        v-for="item in uploads"
        :key="item.key"
        class="rounded-control border border-line p-3"
        data-testid="upload-row"
      >
        <div class="flex items-center justify-between gap-3">
          <span class="min-w-0 truncate text-sm font-semibold text-ink">{{ item.fileName }}</span>
          <el-button v-if="item.phase === 'failed'" text size="small" @click="dismiss(item.key)">
            {{ t('common.close') }}
          </el-button>
          <el-button
            v-else-if="item.phase === 'uploading'"
            text
            size="small"
            @click="item.cancel()"
          >
            {{ t('common.cancel') }}
          </el-button>
        </div>
        <p v-if="item.error" class="mt-1 text-sm text-danger" role="alert">{{ item.error }}</p>
        <template v-else>
          <el-progress
            :percentage="item.phase === 'processing' ? 100 : item.progress"
            :indeterminate="item.phase === 'processing'"
            :show-text="false"
            class="mt-2"
            data-testid="upload-progress"
          />
          <p class="mt-1 text-xs text-ink-subtle">
            {{
              item.phase === 'processing'
                ? t('admin.upload.processing')
                : t('admin.upload.uploading', { percent: item.progress })
            }}
          </p>
        </template>
      </li>
    </ul>

    <ol
      v-if="images.length"
      class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3"
      data-testid="image-list"
    >
      <li
        v-for="(image, index) in images"
        :key="image.mediaAssetId"
        class="overflow-hidden rounded-card border bg-surface"
        :class="draggedIndex === index ? 'border-primary opacity-60' : 'border-line'"
        :draggable="!disabled"
        data-testid="image-item"
        @dragstart="draggedIndex = index"
        @dragend="draggedIndex = null"
        @dragover.prevent
        @drop.prevent.stop="onThumbDrop(index)"
      >
        <div class="relative aspect-[4/3] bg-canvas">
          <img
            v-if="safeUrl(image.medium ?? image.thumbnail)"
            :src="safeUrl(image.medium ?? image.thumbnail)"
            :alt="image.altText"
            class="size-full object-contain"
            data-testid="image-thumb"
          />
          <span
            v-if="index === 0"
            class="absolute left-2 top-2 rounded bg-primary px-2 py-0.5 text-xs font-semibold text-on-primary"
          >
            {{ t('admin.upload.cover') }}
          </span>
        </div>
        <div class="space-y-2 p-2">
          <el-input
            v-model="image.altText"
            :placeholder="t('admin.upload.altText')"
            :aria-label="t('admin.upload.altText')"
            size="small"
            maxlength="300"
          />
          <div v-if="!disabled" class="flex items-center gap-1">
            <el-button
              :icon="ArrowLeft"
              size="small"
              :disabled="index === 0"
              :aria-label="t('admin.upload.moveEarlier')"
              data-testid="image-earlier"
              @click="move(index, index - 1)"
            />
            <el-button
              :icon="ArrowRight"
              size="small"
              :disabled="index === images.length - 1"
              :aria-label="t('admin.upload.moveLater')"
              data-testid="image-later"
              @click="move(index, index + 1)"
            />
            <el-button
              class="!ml-auto"
              :icon="Delete"
              size="small"
              type="danger"
              plain
              :aria-label="t('admin.upload.remove')"
              data-testid="image-remove"
              @click="removeImage(index)"
            />
          </div>
        </div>
      </li>
    </ol>
    <p v-else-if="disabled" class="text-sm text-ink-muted">{{ t('admin.upload.none') }}</p>
  </div>
</template>
