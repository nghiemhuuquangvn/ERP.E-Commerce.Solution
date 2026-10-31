import { describe, it, expect, vi } from 'vitest'
import { useState } from 'react'
import { render, screen } from '@testing-library/react'
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
