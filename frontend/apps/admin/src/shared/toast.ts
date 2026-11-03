import { ElMessage } from 'element-plus'
import 'element-plus/es/components/message/style/css'

/** A short note that something worked. Failures stay inside the form or dialog they belong to. */
export function toastSuccess(message: string): void {
  ElMessage({ message, type: 'success', duration: 3000, grouping: true })
}

export function toastError(message: string): void {
  ElMessage({ message, type: 'error', duration: 6000, showClose: true })
}

export function toastWarning(message: string, duration = 6000): void {
  ElMessage({ message, type: 'warning', duration, showClose: true })
}
