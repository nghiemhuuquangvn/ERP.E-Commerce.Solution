import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ComponentType, ReactElement } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// The identity API files use the gateway client: both clients are the same fake here.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { RolesPermissionsPage } from './RolesPermissionsPage'
import { TenantsPage } from './TenantsPage'
import { UsersPage } from './UsersPage'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)
const del = vi.mocked(apiClient.delete)

function renderAt(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>{ui}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}
const page = (Page: ComponentType) => <Page />

const emptyList = () => Object.assign([], { items: [], totalCount: 0, total: 0 })
/** Answer by url: the first matching rule wins, everything else is an empty list. */
function answer(rules: Array<[RegExp, unknown]>) {
  get.mockImplementation(((url: string) => {
    const hit = rules.find(([re]) => re.test(url))
    return Promise.resolve({ data: hit ? hit[1] : emptyList() })
  }) as never)
}

const ALL = [
  'identity:users:manage', 'identity:users:edit', 'identity:users:delete', 'identity:tenants:view',
  'identity:tenants:create', 'identity:tenants:edit', 'identity:tenants:manage',
  'identity:roles:create', 'identity:roles:edit', 'identity:roles:delete', 'identity:roles:manage',
]

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  del.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: emptyList() })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  del.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles: ['Admin'], permissions: ALL },
    isAuthenticated: true,
  })
})

const submitOf = (dialog: HTMLElement) => within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!

const roleRow = { id: 'r1', roleCode: 'SALES', roleName: 'Sales rep', isActive: true }
const tenantRow = { id: 'tn1', tenantCode: 'ACME', tenantName: 'Acme Ltd', status: 1 }
const user = (id: string, email: string, extra: Record<string, unknown> = {}) => ({
  id, email, userName: null, fullName: null, isActive: true, isLocked: false, lastLoginAt: null, roles: [], permissions: [], roleAssignments: [], ...extra,
})

describe.each([
  ['Tenants', TenantsPage, /Add Tenant/],
  ['Roles', RolesPermissionsPage, /Add Role/],
] as Array<[string, ComponentType, RegExp]>)('Identity: %s', (_n, Page, addName) => {
  it('shows the empty state, opens the add dialog, shows linked required errors, is accessible, and closes with Esc', async () => {
    renderAt(page(Page))
    await userEvent.click(await screen.findByRole('button', { name: addName }))
    const dialog = await screen.findByRole('dialog')
    expect(document.body.style.overflow).toBe('hidden')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(submitOf(dialog))
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    for (const control of dialog.querySelectorAll('[aria-invalid="true"]')) {
      expect(document.getElementById(control.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })
})

describe('Identity: tenants', () => {
  it('deactivating asks first and sends nothing until confirmed', async () => {
    answer([[/tenants$/, [tenantRow]]])
    renderAt(page(TenantsPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Deactivate Acme Ltd' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/Acme Ltd/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(post).not.toHaveBeenCalled()
  })
})

describe('Identity: users', () => {
  const inactive = user('u2', 'gone@shop.test', { isActive: false })

  it('Activate is offered on an inactive user, asks first and posts only after the confirm', async () => {
    answer([[/users$/, { items: [inactive, user('u3', 'ok@shop.test')], totalCount: 2 }]])
    renderAt(page(UsersPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Activate gone@shop.test' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/sign in again/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Activate gone@shop.test' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Activate' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/users/u2/activate')))
    expect(screen.queryByRole('button', { name: 'Activate ok@shop.test' })).toBeNull()
  })

  it('a refusal of the server is shown as a toast', async () => {
    answer([[/users$/, { items: [inactive], totalCount: 1 }]])
    post.mockImplementationOnce((() => Promise.reject({ response: { status: 400, data: { detail: 'Not allowed.' } } })) as never)
    renderAt(page(UsersPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Activate gone@shop.test' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Activate' }))
    expect(await screen.findByText('Not allowed.')).toBeInTheDocument()
  })

  it('Activate is not offered on your own row', async () => {
    answer([[/users$/, { items: [user('me', 'me@b.c', { isActive: false })], totalCount: 1 }]])
    renderAt(page(UsersPage))
    expect(await screen.findByText('me@b.c')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Activate/ })).toBeNull()
  })

  it('looking at another tenant is read-only: a notice, no action buttons; back to My Tenant they return', async () => {
    answer([[/tenants$/, [tenantRow]], [/users$/, { items: [user('u3', 'ok@shop.test')], totalCount: 1 }]])
    renderAt(page(UsersPage))
    await screen.findByRole('button', { name: /Lock ok@shop.test/ })
    await userEvent.selectOptions(await screen.findByLabelText('Tenant'), await screen.findByRole('option', { name: 'Acme Ltd' }))
    expect(await screen.findByText(/looking at another tenant/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Lock ok@shop.test/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /Invite/ })).toBeNull()
    await userEvent.selectOptions(screen.getByLabelText('Tenant'), screen.getByRole('option', { name: 'My Tenant' }))
    expect(await screen.findByRole('button', { name: /Lock ok@shop.test/ })).toBeInTheDocument()
    expect(screen.queryByText(/looking at another tenant/i)).toBeNull()
  })
})

describe('Identity: roles', () => {
  it('looking at another tenant hides Add, Edit, Delete and the permissions button', async () => {
    answer([[/tenants$/, [tenantRow]], [/roles$/, [roleRow]]])
    renderAt(page(RolesPermissionsPage))
    await screen.findByRole('button', { name: 'Delete Sales rep' })
    await userEvent.selectOptions(await screen.findByLabelText('Tenant'), await screen.findByRole('option', { name: 'Acme Ltd' }))
    expect(await screen.findByText(/looking at another tenant/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete Sales rep' })).toBeNull()
    expect(screen.queryByRole('button', { name: /Add Role/ })).toBeNull()
  })

  it('deleting a role states the consequence and sends nothing until confirmed', async () => {
    answer([[/roles$/, [roleRow]]])
    renderAt(page(RolesPermissionsPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Delete Sales rep' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/loses its permissions at once/)).toBeInTheDocument()
    expect(del).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(del).toHaveBeenCalledWith(expect.stringContaining('/roles/r1')))
  })

  it('ticking a permission saves it at once, filters by text, and a refusal shows the server reason', async () => {
    const catalog = [
      { id: 'p1', permissionCode: 'sales:orders:view', permissionName: 'View orders', module: 'Sales' },
      { id: 'p2', permissionCode: 'hrm:leave:view', permissionName: 'View leave', module: 'HRM' },
    ]
    answer([[/roles\/r1\/permissions/, ['hrm:leave:view']], [/roles$/, [roleRow]], [/permissions$/, catalog]])
    renderAt(page(RolesPermissionsPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Permissions Sales rep' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    expect(await within(dialog).findByRole('checkbox', { name: 'View leave' })).toBeChecked()
    expect(within(dialog).getByText(/1 of 1 granted/)).toBeInTheDocument()

    await userEvent.type(within(dialog).getByRole('searchbox'), 'orders')
    expect(within(dialog).queryByRole('checkbox', { name: 'View leave' })).toBeNull()

    post.mockRejectedValueOnce({ response: { status: 400, data: { detail: 'Permission is platform-only.' } } })
    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'View orders' }))
    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(await screen.findByText('Permission is platform-only.')).toBeInTheDocument()
  })
})

describe('Identity: users', () => {
  const list = {
    items: [user('me', 'me@b.c', { roles: ['Admin'] }), user('u2', 'other@b.c', { roles: ['Staff'] })],
    totalCount: 2, page: 1, pageSize: 20,
  }

  it('offers Lock and Deactivate on other accounts but never on your own row', async () => {
    answer([[/\/users/, list]])
    renderAt(page(UsersPage))
    expect(await screen.findByRole('button', { name: 'Lock other@b.c' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Deactivate other@b.c' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Lock me@b.c' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Deactivate me@b.c' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Edit me@b.c' })).toBeInTheDocument()
  })

  it('Lock and Deactivate say what happens and send nothing before the confirmation', async () => {
    answer([[/\/users/, list]])
    renderAt(page(UsersPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Lock other@b.c' }))
    let dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/signed out everywhere at once/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    await userEvent.click(screen.getByRole('button', { name: 'Deactivate other@b.c' }))
    dialog = await screen.findByRole('dialog')
    expect(del).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Deactivate' }))
    await waitFor(() => expect(del).toHaveBeenCalledWith(expect.stringContaining('/users/u2')))
  })

  it('a refused Lock is reported with the server reason', async () => {
    answer([[/\/users/, list]])
    post.mockRejectedValueOnce({ response: { status: 400, data: { detail: 'Cannot lock this account.' } } })
    renderAt(page(UsersPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Lock other@b.c' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Lock' }))
    expect(await screen.findByText('Cannot lock this account.')).toBeInTheDocument()
  })

  it('Invite explains it adds an existing account, validates the e-mail, and shows the server reason', async () => {
    answer([[/\/users/, list]])
    renderAt(page(UsersPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Invite User' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/must already have an account/)).toBeInTheDocument()
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(submitOf(dialog))
    await waitFor(() => expect(dialog.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(post).not.toHaveBeenCalled()
    post.mockRejectedValueOnce({ response: { status: 404, data: { detail: "No account found for email 'x@y.z'." } } })
    await userEvent.type(within(dialog).getByRole('textbox'), 'x@y.z')
    await userEvent.click(submitOf(dialog))
    expect(await within(dialog).findByText("No account found for email 'x@y.z'.")).toBeInTheDocument()
  })

  it('Edit profile and Manage roles dialogs are accessible and close with Esc', async () => {
    answer([[/\/users/, { ...list, items: [user('u2', 'other@b.c', { roleAssignments: [{ roleId: 'r1', roleCode: 'SALES', roleName: 'Sales rep' }], roles: ['Sales rep'] })] }]])
    renderAt(page(UsersPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Edit other@b.c' }))
    expect(await axe(await screen.findByRole('dialog'))).toHaveNoViolations()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    await userEvent.click(screen.getByRole('button', { name: 'Manage Roles other@b.c' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    expect(within(dialog).getByRole('button', { name: 'Remove role Sales rep' })).toBeInTheDocument()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })
})

describe('Identity: translations', () => {
  it('every key the Identity pages use exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const [file, src] of Object.entries(files)) {
      if (file.endsWith('.test.tsx')) continue
      const keys = [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])
      for (const k of keys) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
