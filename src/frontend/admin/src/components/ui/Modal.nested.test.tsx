import { describe, it, expect, vi } from 'vitest'
import { useState } from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Button } from './Button'
import { Modal } from './Modal'

function Nested({ onOuterClose = vi.fn() }: { onOuterClose?: () => void }) {
  const [inner, setInner] = useState(false)
  return (
    <Modal open title="Outer" onClose={onOuterClose}>
      <Button onClick={() => setInner(true)}>Add products</Button>
      <Modal open={inner} title="Inner" onClose={() => setInner(false)} footer={<Button>Pick</Button>}>
        <input aria-label="Search" />
      </Modal>
    </Modal>
  )
}

describe('nested Modal (a picker opened from a form)', () => {
  it('Esc closes only the inner dialog, and the focus goes back to the button that opened it', async () => {
    const onOuterClose = vi.fn()
    render(<Nested onOuterClose={onOuterClose} />)
    const opener = screen.getByRole('button', { name: 'Add products' })
    await userEvent.click(opener)
    expect(screen.getByRole('dialog', { name: 'Inner' })).toBeInTheDocument()
    expect(screen.getByLabelText('Search')).toHaveFocus()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: 'Inner' })).toBeNull()
    expect(onOuterClose).not.toHaveBeenCalled()
    expect(opener).toHaveFocus()
    expect(screen.getByRole('dialog', { name: 'Outer' })).toBeInTheDocument()
  })

  it('when the opener was drawn again, the focus goes to the dialog below so Esc still works', async () => {
    const onOuterClose = vi.fn()
    function Redrawn() {
      const [inner, setInner] = useState(false)
      const [key, setKey] = useState(0)
      return (
        <Modal open title="Outer" onClose={onOuterClose}>
          <Button key={key} onClick={() => setInner(true)}>Open inner</Button>
          <Modal open={inner} title="Inner" onClose={() => { setInner(false); setKey((k) => k + 1) }}>
            <input aria-label="Search" />
          </Modal>
        </Modal>
      )
    }
    render(<Redrawn />)
    await userEvent.click(screen.getByRole('button', { name: 'Open inner' }))
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.getByRole('dialog', { name: 'Outer' }).contains(document.activeElement)).toBe(true))
    await userEvent.keyboard('{Escape}')
    expect(onOuterClose).toHaveBeenCalledTimes(1)
  })

  it('Tab stays inside the inner dialog', async () => {
    render(<Nested />)
    await userEvent.click(screen.getByRole('button', { name: 'Add products' }))
    const inner = screen.getByRole('dialog', { name: 'Inner' })
    for (let i = 0; i < 6; i++) {
      await userEvent.tab()
      expect(inner.contains(document.activeElement)).toBe(true)
    }
  })
})
