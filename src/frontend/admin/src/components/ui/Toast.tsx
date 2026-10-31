import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { AlertCircle, CheckCircle2, Info, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cx } from './cx'

export type ToastKind = 'success' | 'error' | 'info'
interface ToastItem {
  id: number
  kind: ToastKind
  message: string
}

const MAX_TOASTS = 3
/** A success or info toast disappears by itself; an error stays until it is closed. */
export const TOAST_DURATION_MS = 4000

interface ToastApi {
  success: (message: string) => void
  error: (message: string) => void
  info: (message: string) => void
  dismiss: (id: number) => void
}
const ToastContext = createContext<ToastApi | null>(null)

const STYLE: Record<ToastKind, string> = {
  success: 'border-success/30 bg-success-soft text-success-soft-text',
  error: 'border-danger/30 bg-danger-soft text-danger-soft-text',
  info: 'border-info/30 bg-info-soft text-info-soft-text',
}
const ICON = { success: CheckCircle2, error: AlertCircle, info: Info }

function ToastView({ toast, onClose }: { toast: ToastItem; onClose: (id: number) => void }) {
  const { t } = useTranslation()
  const Icon = ICON[toast.kind]
  useEffect(() => {
    if (toast.kind === 'error') return
    const timer = setTimeout(() => onClose(toast.id), TOAST_DURATION_MS)
    return () => clearTimeout(timer)
  }, [toast.id, toast.kind, onClose])
  return (
    <div role={toast.kind === 'error' ? 'alert' : 'status'} className={cx('pointer-events-auto flex w-80 items-start gap-3 rounded-md border px-3 py-2 text-sm shadow-lg', STYLE[toast.kind])}>
      <Icon size={18} strokeWidth={1.75} aria-hidden="true" className="mt-0.5 shrink-0" />
      <p className="min-w-0 flex-1 break-words">{toast.message}</p>
      <button type="button" onClick={() => onClose(toast.id)} aria-label={t('common.close')} className="rounded p-0.5 hover:bg-black/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action">
        <X size={16} strokeWidth={1.75} aria-hidden="true" />
      </button>
    </div>
  )
}

/** Mount once in the app shell. Pages call `useToast()`; never build a toast inline. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([])
  const nextId = useRef(1)

  const dismiss = useCallback((id: number) => setToasts((list) => list.filter((x) => x.id !== id)), [])
  const push = useCallback((kind: ToastKind, message: string) => {
    setToasts((list) => [...list, { id: nextId.current++, kind, message }].slice(-MAX_TOASTS))
  }, [])

  const api = useMemo<ToastApi>(
    () => ({
      success: (m) => push('success', m),
      error: (m) => push('error', m),
      info: (m) => push('info', m),
      dismiss,
    }),
    [push, dismiss],
  )

  return (
    <ToastContext.Provider value={api}>
      {children}
      {createPortal(
        <div className="pointer-events-none fixed right-4 top-4 z-[60] flex flex-col gap-2">
          {toasts.map((x) => <ToastView key={x.id} toast={x} onClose={dismiss} />)}
        </div>,
        document.body,
      )}
    </ToastContext.Provider>
  )
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider> (mounted in App.tsx)')
  return ctx
}
