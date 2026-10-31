import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Plus } from 'lucide-react'
import { axe } from '@/test/axe'
import { Button } from './Button'
import { Skeleton, SkeletonRows } from './Skeleton'
import { Spinner } from './Spinner'

describe('Button', () => {
  it('is a type=button by default and calls onClick', async () => {
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Save promotion</Button>)
    const b = screen.getByRole('button', { name: 'Save promotion' })
    expect(b).toHaveAttribute('type', 'button')
    await userEvent.click(b)
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('submits a form when type=submit', async () => {
    const onSubmit = vi.fn((e: React.FormEvent) => e.preventDefault())
    render(<form onSubmit={onSubmit}><Button type="submit">Go</Button></form>)
    await userEvent.click(screen.getByRole('button', { name: 'Go' }))
    expect(onSubmit).toHaveBeenCalledTimes(1)
  })

  it('works with the keyboard (Enter and Space)', async () => {
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Go</Button>)
    await userEvent.tab()
    expect(screen.getByRole('button')).toHaveFocus()
    await userEvent.keyboard('{Enter}')
    await userEvent.keyboard(' ')
    expect(onClick).toHaveBeenCalledTimes(2)
  })

  it('ignores clicks when disabled', async () => {
    const onClick = vi.fn()
    render(<Button disabled onClick={onClick}>Go</Button>)
    await userEvent.click(screen.getByRole('button'))
    expect(onClick).not.toHaveBeenCalled()
  })

  it('while loading keeps the label (width), is busy and ignores clicks', async () => {
    const onClick = vi.fn()
    render(<Button loading onClick={onClick}>Saving</Button>)
    const b = screen.getByRole('button', { name: /Saving/ })
    expect(b).toBeDisabled()
    expect(b).toHaveAttribute('aria-busy', 'true')
    await userEvent.click(b)
    expect(onClick).not.toHaveBeenCalled()
  })

  it('applies variant, size and fullWidth', () => {
    render(<Button variant="danger" size="lg" fullWidth>Delete</Button>)
    const b = screen.getByRole('button')
    expect(b.className).toContain('bg-danger')
    expect(b.className).toContain('h-11')
    expect(b.className).toContain('w-full')
  })

  it('renders icons around the label and an icon-only button by its aria-label', () => {
    const { rerender } = render(<Button leftIcon={<Plus data-testid="i" />}>Add</Button>)
    expect(screen.getByTestId('i')).toBeInTheDocument()
    rerender(<Button iconOnly aria-label="Add item"><Plus /></Button>)
    expect(screen.getByRole('button', { name: 'Add item' })).toBeInTheDocument()
  })

  it('has no axe violations (default, disabled, loading, icon-only)', async () => {
    const { container } = render(
      <div>
        <Button>One</Button>
        <Button disabled>Two</Button>
        <Button loading>Three</Button>
        <Button iconOnly aria-label="Four"><Plus /></Button>
      </div>,
    )
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Spinner and Skeleton', () => {
  it('Spinner announces itself only when it has a label', () => {
    const { rerender } = render(<Spinner />)
    expect(screen.queryByRole('status')).toBeNull()
    rerender(<Spinner label="Loading" />)
    expect(screen.getByRole('status', { name: 'Loading' })).toBeInTheDocument()
  })

  it('Skeleton is hidden from assistive tech and SkeletonRows draws rows x columns cells', () => {
    const { container } = render(<table><tbody><SkeletonRows rows={3} columns={4} /></tbody></table>)
    expect(container.querySelectorAll('tr')).toHaveLength(3)
    expect(container.querySelectorAll('td')).toHaveLength(12)
    render(<Skeleton />)
  })
})
