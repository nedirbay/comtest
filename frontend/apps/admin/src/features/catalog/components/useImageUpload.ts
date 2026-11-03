import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { useApiErrorMessage } from '@comtest/ui'
import { computed, onBeforeUnmount, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'

/** The same limits as `MediaRules` on the server, to save a pointless upload. It still checks. */
export const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const
export const MAX_IMAGE_BYTES = 10 * 1024 * 1024

const POLL_INTERVAL_MS = 1000
const POLL_ATTEMPTS = 90

export type UploadPhase = 'uploading' | 'processing' | 'failed'

export interface Upload {
  key: number
  fileName: string
  phase: UploadPhase
  progress: number
  error: string | null
  cancel: () => void
}

/** Null when the file may go, or the i18n key of what is wrong with it. */
export function imageFileProblem(file: { type: string; size: number }): string | null {
  if (!(ALLOWED_IMAGE_TYPES as readonly string[]).includes(file.type)) {
    return 'admin.upload.wrongType'
  }

  return file.size > MAX_IMAGE_BYTES ? 'admin.upload.tooLarge' : null
}

class UploadCancelled extends Error {}

/**
 * The file goes straight to storage on a signed address, so that a large upload does not tie up
 * the API. XMLHttpRequest because `fetch` still cannot report upload progress.
 */
function putFile(
  url: string,
  file: File,
  contentType: string,
  onProgress: (percent: number) => void,
  onStart: (abort: () => void) => void,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const request = new XMLHttpRequest()

    request.open('PUT', url)
    request.setRequestHeader('Content-Type', contentType)
    request.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        onProgress(Math.round((event.loaded / event.total) * 100))
      }
    }
    request.onload = () =>
      request.status >= 200 && request.status < 300
        ? resolve()
        : reject(new Error(`Upload failed with ${request.status}`))
    request.onerror = () => reject(new Error('Upload failed'))
    request.onabort = () => reject(new UploadCancelled())
    onStart(() => request.abort())
    request.send(file)
  })
}

export function useImageUpload(onReady: (asset: Schema<'MediaAssetDto'>) => void) {
  const { t } = useI18n({ useScope: 'global' })
  const api = useApi()
  const describeError = useApiErrorMessage()
  const uploads = ref<Upload[]>([])
  let nextKey = 1
  let unmounted = false

  onBeforeUnmount(() => {
    unmounted = true
    uploads.value.forEach((upload) => upload.cancel())
  })

  const remove = (key: number) => {
    uploads.value = uploads.value.filter((upload) => upload.key !== key)
  }

  async function waitUntilProcessed(id: string): Promise<Schema<'MediaAssetDto'>> {
    for (let attempt = 0; attempt < POLL_ATTEMPTS && !unmounted; attempt += 1) {
      const asset = await unwrap(
        api.client.GET('/api/v1/admin/media/{id}', { params: { path: { id } } }),
      )
      const status = asset.status.toLowerCase()

      if (status === 'ready' || status === 'rejected') {
        return asset
      }

      await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS))
    }

    throw new Error(t('admin.upload.timeout'))
  }

  async function upload(file: File): Promise<void> {
    const entry = reactive<Upload>({
      key: nextKey++,
      fileName: file.name,
      phase: 'uploading',
      progress: 0,
      error: null,
      cancel: () => undefined,
    })

    uploads.value = [...uploads.value, entry]

    const problem = imageFileProblem(file)

    if (problem) {
      entry.phase = 'failed'
      entry.error = t(problem)

      return
    }

    try {
      const ticket = await unwrap(
        api.client.POST('/api/v1/admin/media/uploads', {
          body: { fileName: file.name, contentType: file.type, sizeBytes: file.size },
        }),
      )

      await putFile(
        ticket.uploadUrl,
        file,
        ticket.contentType,
        (percent) => {
          entry.progress = percent
        },
        (abort) => {
          entry.cancel = abort
        },
      )

      entry.phase = 'processing'
      entry.cancel = () => undefined

      await unwrap(
        api.client.POST('/api/v1/admin/media/{id}/complete', {
          params: { path: { id: ticket.id } },
        }),
      )

      const asset = await waitUntilProcessed(ticket.id)

      if (asset.status.toLowerCase() === 'rejected') {
        entry.phase = 'failed'
        entry.error = t('admin.upload.rejected')

        return
      }

      remove(entry.key)
      onReady(asset)
    } catch (error) {
      if (error instanceof UploadCancelled) {
        remove(entry.key)
      } else {
        entry.phase = 'failed'
        entry.error =
          error instanceof Error && error.message.startsWith('Upload failed')
            ? t('admin.upload.failed')
            : describeError(error)
      }
    }
  }

  return {
    uploads,
    upload,
    dismiss: remove,
    busy: computed(() => uploads.value.some((item) => item.phase !== 'failed')),
  }
}
