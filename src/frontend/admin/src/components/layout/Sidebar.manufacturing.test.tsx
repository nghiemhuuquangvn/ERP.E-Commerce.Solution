import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { useAuthStore } from '@/stores/auth.store'

const as = (permissions: string[]) => useAuthStore.setState({ user: { id: 'u', email: 'a@b.c', tenantId: 't', roles: [], permissions }, isAuthenticated: true } as never)
const show = (path = '/manufacturing/boms') => render(<MemoryRouter initialEntries={[path]}><Sidebar /></MemoryRouter>)

describe('Manufacturing menu', () => {
  beforeEach(() => as([]))

  it('is hidden from a user with no manufacturing permission', () => {
    show()
    expect(screen.queryByText('Manufacturing')).toBeNull()
  })

  it('shows only the entries the permissions allow', () => {
    as(['manufacturing:bom:view'])
    show()
    expect(screen.getByText('Manufacturing')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Bills of materials' })).toHaveAttribute('href', '/manufacturing/boms')
    expect(screen.getByRole('link', { name: 'Where used' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Routings' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Work centers' })).toBeNull()
  })

  it('shows the master data entries to a holder of the master data permission', () => {
    as(['manufacturing:masterdata:view', 'manufacturing:routing:view'])
    show('/manufacturing/plants')
    for (const name of ['Routings', 'Work centers', 'Shifts and calendars', 'Plants', 'Production settings', 'Item profiles']) expect(screen.getByRole('link', { name })).toBeInTheDocument()
  })

  it('shows Inspection plans only to a holder of the plan view permission (P4-3)', () => {
    as(['manufacturing:bom:view'])
    show()
    expect(screen.queryByRole('link', { name: 'Inspection plans' })).toBeNull()

    as(['manufacturing:qc:plan-view'])
    show('/manufacturing/inspection-plans')
    expect(screen.getByRole('link', { name: 'Inspection plans' })).toHaveAttribute('href', '/manufacturing/inspection-plans')
  })
})
