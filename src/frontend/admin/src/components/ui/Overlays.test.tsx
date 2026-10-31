import { describe, it, expect, vi, afterEach, beforeEach } from 'vitest'
import { useState } from 'react'
import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'
import { Alert } from './Alert'
import { Button } from './Button'
import { Modal } from './Modal'
import { ToastProvider, useToast, TOAST_DURATION_MS } from './Toast'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ReasonDialog } from '@/components/shared/ReasonDialog'

function Harness({ onClose = vi.fn(), dismissOnBackdrop = false }: { onClose?: () => void; dismissOnBackdrop?: boolean }) {
  return (
    <Modal open title="Edit promotion" onClose={onClose} dismissOnBackdrop={dismissOnBackdrop} footer={<Button>Save</Button>}>
      <input aria-label="Name" />
    </Modal>
  )
}

describe('Modal', () => {
  it('is a labelled modal dialog in a portal, with the first field focused', () => {
    render(<Harness />)
    const dialog = screen.getByRole('dialog', { name: 'Edit promotion' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(document.body.contains(dialog)).toBe(true)
    expect(screen.getByLabelText('Name')).toHaveFocus()
  })

  it('renders nothing when closed', () => {
    render(<Modal open={false} title="x" onClose={() => {}}>body</Modal>)
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('closes on Esc and with the close button, but not on a backdrop click by default', async () => {
    const onClose = vi.fn()
    render(<Harness onClose={onClose} />)
    await userEvent.keyboard('{Escape}')
    expect(onClose).toHaveBeenCalledTimes(1)
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(onClose).toHaveBeenCalledTimes(2)
    fireEvent.mouseDown(screen.getByRole('dialog').parentElement!)
    expect(onClose).toHaveBeenCalledTimes(2)
  })

  it('closes on a backdrop click when dismissOnBackdrop, not on a click inside', () => {
    const onClose = vi.fn()
    render(<Harness onClose={onClose} dismissOnBackdrop />)
    fireEvent.mouseDown(screen.getByLabelText('Name'))
    expect(onClose).not.toHaveBeenCalled()
    fireEvent.mouseDown(screen.getByRole('dialog').parentElement!)
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('traps Tab inside the dialog in both directions', async () => {
    render(<Harness />)
    const close = screen.getByRole('button', { name: 'Close' })
    const save = screen.getByRole('button', { name: 'Save' })
    save.focus()
    await userEvent.tab()
    expect(close).toHaveFocus()
    await userEvent.tab({ shift: true })
    expect(save).toHaveFocus()
  })

  it('locks the page scroll while open and returns the focus to the trigger on close', async () => {
    function Page() {
      return (
        <ToastlessPage />
      )
    }
    function ToastlessPage() {
      const [open, setOpen] = useState(false)
      return (
        <>
          <button onClick={() => setOpen(true)}>Open</button>
          <Modal open={open} title="T" onClose={() => setOpen(false)}>body</Modal>
        </>
      )
    }
    render(<Page />)
    const trigger = screen.getByRole('button', { name: 'Open' })
    await userEvent.click(trigger)
    expect(document.body.style.overflow).toBe('hidden')
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.body.style.overflow).not.toBe('hidden')
    expect(trigger).toHaveFocus()
  })

  it('has no axe violations', async () => {
    render(<Harness />)
    expect(await axe(document.body)).toHaveNoViolations()
  })
})

describe('Alert', () => {
  it('announces danger at once and other variants politely', () => {
    const { rerender } = render(<Alert variant="danger">Refused</Alert>)
    expect(screen.getByRole('alert')).toHaveTextContent('Refused')
    rerender(<Alert variant="warning" title="Careful">Check</Alert>)
    expect(screen.getByRole('status')).toHaveTextContent('Careful')
  })

  it('can be dismissed', async () => {
    const onClose = vi.fn()
    render(<Alert onClose={onClose}>Hi</Alert>)
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(onClose).toHaveBeenCalled()
  })

  it('has no axe violations', async () => {
    const { container } = render(<div><Alert variant="danger">Refused</Alert><Alert variant="success">Done</Alert></div>)
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Toast', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  function Trigger() {
    const toast = useToast()
    return (
      <>
        <button onClick={() => toast.success('Saved')}>ok</button>
        <button onClick={() => toast.error('Refused')}>bad</button>
        <button onClick={() => toast.info('FYI')}>info</button>
      </>
    )
  }
  const setup = () => render(<ToastProvider><Trigger /></ToastProvider>)

  it('a success toast is a status and disappears after 4 s', () => {
    setup()
    fireEvent.click(screen.getByText('ok'))
    expect(screen.getByRole('status')).toHaveTextContent('Saved')
    act(() => { vi.advanceTimersByTime(TOAST_DURATION_MS - 1) })
    expect(screen.queryByText('Saved')).not.toBeNull()
    act(() => { vi.advanceTimersByTime(2) })
    expect(screen.queryByText('Saved')).toBeNull()
  })

  it('an error toast is an alert and stays until it is closed', () => {
    setup()
    fireEvent.click(screen.getByText('bad'))
    expect(screen.getByRole('alert')).toHaveTextContent('Refused')
    act(() => { vi.advanceTimersByTime(60_000) })
    expect(screen.getByRole('alert')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('stacks at most 3 (the oldest goes first)', () => {
    setup()
    for (let i = 0; i < 4; i++) fireEvent.click(screen.getByText('bad'))
    expect(screen.getAllByRole('alert')).toHaveLength(3)
    fireEvent.click(screen.getByText('info'))
    expect(screen.getAllByRole('alert')).toHaveLength(2)
    expect(screen.getByRole('status')).toHaveTextContent('FYI')
  })

  it('useToast outside the provider explains itself', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<Trigger />)).toThrow(/ToastProvider/)
    spy.mockRestore()
  })
})

describe('ConfirmDialog (rebased on Modal)', () => {
  it('keeps its props: confirm, cancel, danger label and default label', async () => {
    const onConfirm = vi.fn()
    const onCancel = vi.fn()
    const { rerender } = render(<ConfirmDialog open title="Delete" message="Sure?" onConfirm={onConfirm} onCancel={onCancel} />)
    expect(screen.getByRole('dialog', { name: 'Delete' })).toHaveTextContent('Sure?')
    await userEvent.click(screen.getByRole('button', { name: 'Confirm' }))
    expect(onConfirm).toHaveBeenCalledTimes(1)
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(onCancel).toHaveBeenCalledTimes(1)
    await userEvent.keyboard('{Escape}')
    expect(onCancel).toHaveBeenCalledTimes(2)
    rerender(<ConfirmDialog open title="Delete" message="Sure?" confirmLabel="Remove" danger onConfirm={onConfirm} onCancel={onCancel} />)
    expect(screen.getByRole('button', { name: 'Remove' }).className).toContain('bg-danger')
    rerender(<ConfirmDialog open={false} title="Delete" message="Sure?" onConfirm={onConfirm} onCancel={onCancel} />)
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('has no axe violations', async () => {
    render(<ConfirmDialog open title="Delete" message="Sure?" onConfirm={() => {}} onCancel={() => {}} />)
    expect(await axe(document.body)).toHaveNoViolations()
  })
})

describe('ReasonDialog (rebased on Modal)', () => {
  it('needs a reason, passes it trimmed with the date, and starts empty when reopened', async () => {
    const onConfirm = vi.fn()
    const { rerender } = render(
      <ReasonDialog open title="Reverse" confirmLabel="Reverse entry" withDate dateLabel="Date" onConfirm={onConfirm} onCancel={() => {}} />,
    )
    const go = screen.getByRole('button', { name: 'Reverse entry' })
    expect(go).toBeDisabled()
    await userEvent.type(screen.getByLabelText('Reason'), '  wrong account ')
    await userEvent.type(screen.getByLabelText('Date'), '2026-10-31')
    await userEvent.click(go)
    expect(onConfirm).toHaveBeenCalledWith('wrong account', '2026-10-31')
    rerender(<ReasonDialog open={false} title="Reverse" confirmLabel="Reverse entry" onConfirm={onConfirm} onCancel={() => {}} />)
    rerender(<ReasonDialog open title="Reverse" confirmLabel="Reverse entry" onConfirm={onConfirm} onCancel={() => {}} />)
    expect(screen.getByLabelText('Reason')).toHaveValue('')
  })

  it('shows a server error as an alert and a busy state that blocks the button and Esc', async () => {
    const onCancel = vi.fn()
    render(<ReasonDialog open title="Reverse" confirmLabel="Go" busy error="Period is closed" onConfirm={() => {}} onCancel={onCancel} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Period is closed')
    expect(screen.getByRole('button', { name: /Go/ })).toBeDisabled()
    await userEvent.keyboard('{Escape}')
    expect(onCancel).not.toHaveBeenCalled()
  })

  it('has no axe violations', async () => {
    render(<ReasonDialog open title="Reverse" confirmLabel="Go" error="Refused" onConfirm={() => {}} onCancel={() => {}} />)
    expect(await axe(document.body)).toHaveNoViolations()
  })
})
