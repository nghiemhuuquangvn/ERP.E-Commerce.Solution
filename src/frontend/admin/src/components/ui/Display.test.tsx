import { describe, it, expect, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'
import { Badge, Button, EmptyState, STATUS_ROLE, statusRole } from '.'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'

describe('Badge and StatusBadge', () => {
  it('shows text with the variant colours and an optional dot', () => {
    render(<Badge variant="success" dot>Active</Badge>)
    const b = screen.getByText('Active')
    expect(b.className).toContain('bg-success-soft')
    expect(b.querySelector('span[aria-hidden="true"]')).not.toBeNull()
  })

  it('maps statuses to the roles of foundations 1.2, and an unknown status to muted without throwing', () => {
    expect(statusRole('Active')).toBe('success')
    expect(statusRole('Delivered')).toBe('success')
    expect(statusRole('Pending')).toBe('warning')
    expect(statusRole('Requested')).toBe('warning')
    expect(statusRole('Cancelled')).toBe('danger')
    expect(statusRole('Processing')).toBe('info')
    expect(statusRole('PickedUp')).toBe('info')
    expect(statusRole('Draft')).toBe('muted')
    expect(statusRole('SomethingNew')).toBe('muted')
  })

  it('every mapped status resolves to a real variant', () => {
    const ok = new Set(['success', 'warning', 'danger', 'info', 'muted', 'brand'])
    for (const [status, role] of Object.entries(STATUS_ROLE)) expect(ok.has(role), status).toBe(true)
  })

  it('StatusBadge shows the translated label when given, else the status', () => {
    const { rerender } = render(<StatusBadge status="Cancelled" label="Đã hủy" />)
    expect(screen.getByText('Đã hủy').className).toContain('bg-danger-soft')
    rerender(<StatusBadge status="Cancelled" />)
    expect(screen.getByText('Cancelled')).toBeInTheDocument()
  })
})

describe('EmptyState', () => {
  it('shows the title, reason and action for each variant', async () => {
    const onClick = vi.fn()
    const { rerender } = render(<EmptyState title="Nothing yet" description="Add the first one" action={<Button onClick={onClick}>Add</Button>} />)
    expect(screen.getByText('Nothing yet')).toBeInTheDocument()
    expect(screen.getByText('Add the first one')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))
    expect(onClick).toHaveBeenCalled()
    for (const v of ['no-results', 'error', 'forbidden'] as const) {
      rerender(<EmptyState variant={v} title={v} />)
      expect(screen.getByText(v)).toBeInTheDocument()
    }
  })

  it('has no axe violations', async () => {
    const { container } = render(<EmptyState variant="error" title="Could not load" description="Try again" action={<Button>Retry</Button>} />)
    expect(await axe(container)).toHaveNoViolations()
  })
})

interface Row { id: string; name: string; total: number }
const rows: Row[] = [{ id: '1', name: 'A', total: 10 }, { id: '2', name: 'B', total: 20 }]
const columns = [
  { key: 'name', label: 'Name', sortable: true },
  { key: 'total', label: 'Total', align: 'right' as const },
]

describe('DataTable (API kept, features added)', () => {
  it('old usage: columns + data render rows; a render function still works', () => {
    render(<DataTable columns={[{ key: 'name', label: 'Name', render: (r: Row) => <b>{r.name}!</b> }]} data={rows} />)
    expect(screen.getByText('A!')).toBeInTheDocument()
    expect(screen.getAllByRole('row')).toHaveLength(3)
  })

  it('shows skeleton rows while loading and marks the table busy', () => {
    const { container } = render(<DataTable columns={columns} data={[]} isLoading />)
    expect(container.querySelectorAll('tbody tr')).toHaveLength(5)
    expect(container.firstChild).toHaveAttribute('aria-busy', 'true')
    expect(screen.queryByText('No records found.')).toBeNull()
  })

  it('shows the default empty state, a custom message and a custom empty state', () => {
    const { rerender } = render(<DataTable columns={columns} data={[]} />)
    expect(screen.getByText('No records found.')).toBeInTheDocument()
    rerender(<DataTable columns={columns} data={[]} emptyMessage="No promotions" />)
    expect(screen.getByText('No promotions')).toBeInTheDocument()
    rerender(<DataTable columns={columns} data={[]} emptyState={<EmptyState variant="no-results" title="Nothing matches" action={<Button>Clear filters</Button>} />} />)
    expect(screen.getByText('Nothing matches')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Clear filters' })).toBeInTheDocument()
  })

  it('right-aligns numeric columns', () => {
    render(<DataTable columns={columns} data={rows} />)
    expect(screen.getByRole('columnheader', { name: 'Total' }).className).toContain('text-right')
    expect(screen.getByText('10').className).toContain('text-right')
  })

  it('sortable headers expose aria-sort and call onSort', async () => {
    const onSort = vi.fn()
    const { rerender } = render(<DataTable columns={columns} data={rows} sortKey="name" sortDir="asc" onSort={onSort} />)
    const header = screen.getByRole('columnheader', { name: /Name/ })
    expect(header).toHaveAttribute('aria-sort', 'ascending')
    await userEvent.click(within(header).getByRole('button'))
    expect(onSort).toHaveBeenCalledWith('name')
    rerender(<DataTable columns={columns} data={rows} sortKey="name" sortDir="desc" onSort={onSort} />)
    expect(screen.getByRole('columnheader', { name: /Name/ })).toHaveAttribute('aria-sort', 'descending')
    expect(screen.getByRole('columnheader', { name: 'Total' })).not.toHaveAttribute('aria-sort')
  })

  it('has no axe violations (rows, loading, empty)', async () => {
    const { container, rerender } = render(<DataTable columns={columns} data={rows} sortKey="name" sortDir="asc" onSort={() => {}} />)
    expect(await axe(container)).toHaveNoViolations()
    rerender(<DataTable columns={columns} data={[]} isLoading />)
    expect(await axe(container)).toHaveNoViolations()
    rerender(<DataTable columns={columns} data={[]} />)
    expect(await axe(container)).toHaveNoViolations()
  })
})
