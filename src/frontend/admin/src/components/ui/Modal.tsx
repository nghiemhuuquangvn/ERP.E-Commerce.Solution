import { useEffect, useId, useRef } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent, ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cx } from './cx'

export type ModalSize = 'sm' | 'md' | 'lg'
const WIDTH: Record<ModalSize, string> = { sm: 'max-w-[400px]', md: 'max-w-[560px]', lg: 'max-w-[800px]' }

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
const FIELDS = 'input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled])'

// Several modals can be open at once (a confirmation over a form): the page scrolls again only when the last closes.
let openModals = 0
let savedOverflow = ''
function lockScroll() {
  if (openModals++ === 0) {
    savedOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
  }
}
function unlockScroll() {
  if (--openModals === 0) document.body.style.overflow = savedOverflow
}

export interface ModalProps {
  open: boolean
  title: string
  onClose: () => void
  size?: ModalSize
  /** Buttons row, pinned under the scrolling body. */
  footer?: ReactNode
  /** Clicking the backdrop closes the dialog. Leave off for forms so a stray click cannot lose typed data. */
  dismissOnBackdrop?: boolean
  /** Esc closes the dialog (on by default; turn off while a request is running). */
  closeOnEsc?: boolean
  className?: string
  children: ReactNode
}

/**
 * The one modal dialog of the Admin: portal, focus trap, Esc, scroll lock, focus returns to the trigger.
 * Never write a `fixed inset-0` block in a page.
 */
export function Modal({ open, title, onClose, size = 'md', footer, dismissOnBackdrop = false, closeOnEsc = true, className, children }: ModalProps) {
  const { t } = useTranslation()
  const titleId = useId()
  const panelRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  useEffect(() => {
    if (!open) return
    const previous = document.activeElement as HTMLElement | null
    lockScroll()
    const panel = panelRef.current
    const target = panel?.querySelector<HTMLElement>(`[data-modal-body] :is(${FIELDS})`) ?? panel?.querySelector<HTMLElement>(FOCUSABLE) ?? panel
    target?.focus()
    return () => {
      unlockScroll()
      if (previous && document.contains(previous)) previous.focus()
    }
  }, [open])

  if (!open) return null

  const onKeyDown = (e: ReactKeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Escape' && closeOnEsc) {
      e.stopPropagation()
      onCloseRef.current()
      return
    }
    if (e.key !== 'Tab') return
    // A dialog opened from inside this one is a React child: it has already handled the key.
    e.stopPropagation()
    const items = Array.from(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? [])
    if (items.length === 0) {
      e.preventDefault()
      return
    }
    const first = items[0]
    const last = items[items.length - 1]
    const active = document.activeElement
    if (e.shiftKey && (active === first || active === panelRef.current)) {
      e.preventDefault()
      last.focus()
    } else if (!e.shiftKey && active === last) {
      e.preventDefault()
      first.focus()
    }
  }

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-ink/45 p-4"
      onMouseDown={(e) => {
        if (dismissOnBackdrop && e.target === e.currentTarget) onCloseRef.current()
      }}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        onKeyDown={onKeyDown}
        className={cx('flex max-h-full w-full flex-col rounded-modal bg-surface shadow-lg outline-none', WIDTH[size], className)}
      >
        <div className="flex items-start justify-between gap-4 px-6 pt-5">
          <h2 id={titleId} className="text-base font-semibold text-ink">{title}</h2>
          <button
            type="button"
            onClick={() => onCloseRef.current()}
            aria-label={t('common.close')}
            className="-mr-2 -mt-1 rounded-md p-1.5 text-ink-muted hover:bg-surface-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action"
          >
            <X size={20} strokeWidth={1.75} aria-hidden="true" />
          </button>
        </div>
        <div data-modal-body className="overflow-y-auto px-6 py-4">{children}</div>
        {footer && <div className="flex justify-end gap-3 border-t border-line px-6 py-4">{footer}</div>}
      </div>
    </div>,
    document.body,
  )
}
