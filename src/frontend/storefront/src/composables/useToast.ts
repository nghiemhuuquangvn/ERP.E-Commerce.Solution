import { readonly, ref } from 'vue'

export type ToastKind = 'success' | 'error' | 'info'
export interface ToastItem {
  id: number
  kind: ToastKind
  message: string
}

const toasts = ref<ToastItem[]>([])
let next = 1

function dismiss(id: number) {
  toasts.value = toasts.value.filter((t) => t.id !== id)
}

function push(kind: ToastKind, message: string) {
  const id = next++
  toasts.value = [...toasts.value, { id, kind, message }]
  // A success or info message goes away by itself; an error stays until it is closed.
  if (kind !== 'error') setTimeout(() => dismiss(id), 4000)
  return id
}

/** Short feedback after an action ("Added to cart"). Rendered by <ToastHost /> in App.vue. */
export function useToast() {
  return {
    toasts: readonly(toasts),
    dismiss,
    success: (message: string) => push('success', message),
    error: (message: string) => push('error', message),
    info: (message: string) => push('info', message),
  }
}
