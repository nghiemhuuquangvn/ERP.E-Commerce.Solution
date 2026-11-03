import { act, fireEvent, screen, waitFor } from '@testing-library/react-native'
import { renderScreen } from './render'

jest.mock('expo-router', () => ({ useRouter: () => ({ push: jest.fn(), replace: jest.fn() }) }))

const mockGet = jest.fn()
const mockPost = jest.fn()
jest.mock('@/api/client', () => ({ apiClient: { get: (...a: unknown[]) => mockGet(...a), post: (...a: unknown[]) => mockPost(...a) } }))

// The camera: the test drives a scan by calling the handler the screen gave to <CameraView>.
const g = global as unknown as { __scan?: (e: { data: string }) => void; __perm: { granted: boolean; canAskAgain: boolean } | null }
jest.mock('expo-camera', () => {
  const React = require('react')
  const { View } = require('react-native')
  return {
    CameraView: (p: { onBarcodeScanned: (e: { data: string }) => void; accessibilityLabel?: string }) => {
      ;(global as unknown as { __scan?: unknown }).__scan = p.onBarcodeScanned
      return React.createElement(View, { accessibilityLabel: p.accessibilityLabel })
    },
    useCameraPermissions: () => [(global as unknown as { __perm: unknown }).__perm, jest.fn()],
  }
})

import POSScreen from '@/app/(tabs)/pos'
import { useAuthStore } from '@/stores/auth.store'
import { usePOSStore } from '@/stores/pos.store'
import { useTerminalStore } from '@/stores/terminal.store'
import { useToastStore } from '@/components/ui/Toast'
import { ToastHost } from '@/components/ui'

const TERM = '11111111-1111-1111-1111-111111111111'
const SHIFT = '22222222-2222-2222-2222-222222222222'
const P1 = { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', name: 'Cozy Sofa', sku: 'SKU-0000010', price: 10, stockQuantity: 5 }
const P2 = { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', name: 'Cozy Lamp', sku: 'SKU-0000011', price: 4, stockQuantity: 0 }

/** Answer each GET by its path; anything not listed is a 404. */
function answer(map: Record<string, unknown>) {
  mockGet.mockImplementation((url: string) => {
    for (const [prefix, value] of Object.entries(map)) {
      if (url.startsWith(prefix)) return value instanceof Error ? Promise.reject(value) : Promise.resolve({ data: value })
    }
    return Promise.reject(Object.assign(new Error('404'), { isAxiosError: true, response: { status: 404 } }))
  })
}
const http = (status: number) => Object.assign(new Error(String(status)), { isAxiosError: true, response: { status } })

const open = () => renderScreen(<><POSScreen /><ToastHost /></>)
const chooseTerminal = () => useTerminalStore.setState({ terminalId: TERM, terminalName: 'Front desk' })
const search = async (text: string) => {
  fireEvent.changeText(screen.getByLabelText('Find a product'), text)
  return screen.findByRole('button', { name: /^Add Cozy Sofa/ })
}

beforeEach(() => {
  jest.clearAllMocks()
  g.__perm = { granted: true, canAskAgain: true }
  g.__scan = undefined
  useAuthStore.setState({ user: { id: 'staff-guid', email: 's@x.y', firstName: 'S', lastName: 'T', roles: [], permissions: [] }, accessToken: 't', isAuthenticated: true })
  usePOSStore.getState().reset()
  useTerminalStore.setState({ terminalId: null, terminalName: null })
  useToastStore.setState({ items: [] })
})

describe('POS terminal', () => {
  it('starts with no terminal and does not let the sale be completed', async () => {
    answer({})
    usePOSStore.getState().addItem({ productId: P1.id, productName: P1.name, unitPrice: 10, taxRate: 10 })
    open()
    expect(screen.getByText('No terminal chosen')).toBeTruthy()
    expect(screen.getByText('Choose a POS terminal before completing the sale.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Complete Transaction' }).props.accessibilityState).toMatchObject({ disabled: true })
  })

  it('lists only the active terminals, remembers the choice and tells whether a cash shift is open', async () => {
    answer({ '/v1/pos/terminals': [{ id: TERM, name: 'Front desk', isActive: true }, { id: 'x', name: 'Old till', isActive: false }], [`/v1/pos/shifts/active/${TERM}`]: { id: SHIFT } })
    open()
    fireEvent.press(screen.getByRole('button', { name: 'Choose' }))
    expect(await screen.findByRole('radio', { name: 'Front desk' })).toBeTruthy()
    expect(screen.queryByRole('radio', { name: 'Old till' })).toBeNull()
    fireEvent.press(screen.getByRole('radio', { name: 'Front desk' }))
    expect(useTerminalStore.getState()).toMatchObject({ terminalId: TERM, terminalName: 'Front desk' })
    expect(await screen.findByText('Cash shift open: the sale is attached to it.')).toBeTruthy()
  })

  it('says so when the terminal has no open cash shift (a 404 or a role that may not read shifts)', async () => {
    answer({})
    chooseTerminal()
    open()
    expect(await screen.findByText('No open cash shift: the sale is not attached to one.')).toBeTruthy()
  })

  it('explains an empty list, a forbidden list and an error with a retry', async () => {
    answer({ '/v1/pos/terminals': [] })
    const { unmount } = open()
    fireEvent.press(screen.getByRole('button', { name: 'Choose' }))
    expect(await screen.findByText('No POS terminal is set up.')).toBeTruthy()
    unmount()

    answer({ '/v1/pos/terminals': http(403) })
    const second = open()
    fireEvent.press(screen.getByRole('button', { name: 'Choose' }))
    expect(await screen.findByText('You are not allowed to see POS terminals.')).toBeTruthy()
    second.unmount()

    answer({ '/v1/pos/terminals': new Error('down') })
    open()
    fireEvent.press(screen.getByRole('button', { name: 'Choose' }))
    expect(await screen.findByText('We could not load this. Please try again.')).toBeTruthy()
    answer({ '/v1/pos/terminals': [{ id: TERM, name: 'Front desk', isActive: true }] })
    fireEvent.press(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('radio', { name: 'Front desk' })).toBeTruthy()
  })
})

describe('POS product search', () => {
  it('waits for two characters, then searches by name or SKU and adds the real product on a tap', async () => {
    answer({ '/v1/products/search': [P1, P2] })
    chooseTerminal()
    open()
    fireEvent.changeText(screen.getByLabelText('Find a product'), 'c')
    expect(await screen.findByText('Type at least 2 characters.')).toBeTruthy()
    expect(mockGet).not.toHaveBeenCalledWith('/v1/products/search', expect.anything())

    const row = await search('cozy')
    expect(mockGet).toHaveBeenCalledWith('/v1/products/search', { params: { searchTerm: 'cozy' } })
    fireEvent.press(row)
    expect(usePOSStore.getState().items).toEqual([{ productId: P1.id, productName: 'Cozy Sofa', unitPrice: 10, quantity: 1, taxRate: 10, sku: 'SKU-0000010' }])
    expect(await screen.findByText('Added Cozy Sofa')).toBeTruthy()
  })

  it('shows a product with no stock but does not let it be picked', async () => {
    answer({ '/v1/products/search': [P1, P2] })
    open()
    await search('cozy')
    const lamp = screen.getByRole('button', { name: /^Add Cozy Lamp.*Out of stock/ })
    expect(lamp.props.accessibilityState).toMatchObject({ disabled: true })
    fireEvent.press(lamp)
    expect(usePOSStore.getState().items).toHaveLength(0)
  })

  it('says when nothing matches', async () => {
    answer({ '/v1/products/search': [] })
    open()
    fireEvent.changeText(screen.getByLabelText('Find a product'), 'zzzz')
    expect(await screen.findByText('No product found.')).toBeTruthy()
  })
})

describe('POS barcode scan', () => {
  const openScanner = async () => {
    open()
    fireEvent.press(screen.getByRole('button', { name: 'Scan a barcode' }))
    await waitFor(() => expect(g.__scan).toBeDefined())
  }
  const scan = (code: string) => act(async () => { g.__scan!({ data: code }) })

  it('adds the product the POS service finds for the barcode', async () => {
    answer({ '/v1/pos/products/lookup/': { productId: P1.id, name: 'Cozy Sofa', sku: 'SKU-0000010', price: 10, isActive: true } })
    await openScanner()
    await scan('SKU-0000010')
    await waitFor(() => expect(usePOSStore.getState().items).toHaveLength(1))
    expect(mockGet).toHaveBeenCalledWith('/v1/pos/products/lookup/SKU-0000010')
    expect(usePOSStore.getState().items[0]).toMatchObject({ productId: P1.id, unitPrice: 10, quantity: 1 })
  })

  it('falls back to the catalogue by exact SKU when the POS cache does not know the barcode', async () => {
    answer({ '/v1/products/search': [{ ...P1, sku: 'SKU-0000010x' }, P1] })
    await openScanner()
    await scan('sku-0000010')
    await waitFor(() => expect(usePOSStore.getState().items).toHaveLength(1))
    expect(usePOSStore.getState().items[0].productId).toBe(P1.id)
  })

  it('tells the cashier when no product has that barcode, and keeps scanning', async () => {
    answer({ '/v1/products/search': [] })
    await openScanner()
    await scan('0000')
    expect(await screen.findByText('No product with the barcode 0000.')).toBeTruthy()
    expect(usePOSStore.getState().items).toHaveLength(0)
    expect(screen.getByLabelText('Camera')).toBeTruthy()
  })

  it('counts one scan once: the same code within two seconds is ignored', async () => {
    answer({ '/v1/pos/products/lookup/': { productId: P1.id, name: 'Cozy Sofa', sku: 'SKU-0000010', price: 10, isActive: true } })
    await openScanner()
    await scan('SKU-0000010')
    await scan('SKU-0000010')
    await waitFor(() => expect(usePOSStore.getState().items).toHaveLength(1))
    expect(usePOSStore.getState().items[0].quantity).toBe(1)
  })

  it('asks for the camera, and points to the settings when it was refused for good', async () => {
    g.__perm = { granted: false, canAskAgain: true }
    open()
    fireEvent.press(screen.getByRole('button', { name: 'Scan a barcode' }))
    expect(await screen.findByText('The camera is needed to scan barcodes.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Allow the camera' })).toBeTruthy()
  })

  it('explains a camera that was turned off in the phone settings', async () => {
    g.__perm = { granted: false, canAskAgain: false }
    open()
    fireEvent.press(screen.getByRole('button', { name: 'Scan a barcode' }))
    expect(await screen.findByText("Camera access is turned off. Allow it in the phone's settings to scan.")).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Open settings' })).toBeTruthy()
  })
})

describe('POS lines and completing a sale', () => {
  const seed = () => usePOSStore.getState().addItem({ productId: P1.id, productName: 'Cozy Sofa', unitPrice: 10, taxRate: 10 })

  it('changes a quantity and removes a line with named buttons; subtotal, 10 % tax and total follow', async () => {
    answer({})
    chooseTerminal()
    seed()
    open()
    fireEvent.press(screen.getByRole('button', { name: 'More Cozy Sofa' }))
    expect(usePOSStore.getState().items[0].quantity).toBe(2)
    expect(screen.getAllByText('$20.00').length).toBeGreaterThan(0)
    expect(screen.getByText('$2.00')).toBeTruthy()
    expect(screen.getByText('$22.00')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Fewer Cozy Sofa' }))
    fireEvent.press(screen.getByRole('button', { name: 'Fewer Cozy Sofa' }))
    expect(usePOSStore.getState().items).toHaveLength(0)
  })

  it('opens the transaction on the chosen terminal with the cash shift, adds real product ids, completes, keeps the terminal', async () => {
    answer({ [`/v1/pos/shifts/active/${TERM}`]: { id: SHIFT } })
    mockPost.mockImplementation((url: string) => Promise.resolve({ data: url === '/v1/pos/transactions' ? { id: 'tx-guid' } : {} }))
    chooseTerminal()
    seed()
    open()
    await screen.findByText('Cash shift open: the sale is attached to it.')
    fireEvent.press(screen.getByRole('radio', { name: 'E-Wallet' }))
    fireEvent.press(screen.getByRole('button', { name: 'Complete Transaction' }))
    expect(await screen.findByText('Transaction complete. Total: $11.00')).toBeTruthy()
    expect(mockPost.mock.calls.map((c) => c[0])).toEqual(['/v1/pos/transactions', '/v1/pos/transactions/tx-guid/items', '/v1/pos/transactions/tx-guid/complete'])
    expect(mockPost.mock.calls[0][1]).toEqual({ terminalId: TERM, cashierId: 'staff-guid', cashShiftId: SHIFT })
    expect(mockPost.mock.calls[1][1]).toEqual({ productId: P1.id, productName: 'Cozy Sofa', quantity: 1, unitPrice: 10, taxRate: 10 })
    expect(mockPost.mock.calls[2][1]).toEqual({ paymentMethod: 'EWallet' })
    expect(usePOSStore.getState().items).toHaveLength(0)
    expect(useTerminalStore.getState().terminalId).toBe(TERM)
  })

  it('sells without a cash shift when none is open (no cashShiftId is sent)', async () => {
    answer({})
    mockPost.mockImplementation((url: string) => Promise.resolve({ data: url === '/v1/pos/transactions' ? { id: 'tx-guid' } : {} }))
    chooseTerminal()
    seed()
    open()
    await screen.findByText('No open cash shift: the sale is not attached to one.')
    fireEvent.press(screen.getByRole('button', { name: 'Complete Transaction' }))
    await waitFor(() => expect(mockPost).toHaveBeenCalledTimes(3))
    expect(mockPost.mock.calls[0][1].cashShiftId).toBeUndefined()
    expect(mockPost.mock.calls[0][1].terminalId).toBe(TERM)
  })

  it('says the user may not ring up sales on a 403, and keeps the lines', async () => {
    answer({})
    mockPost.mockRejectedValue(http(403))
    chooseTerminal()
    seed()
    open()
    fireEvent.press(screen.getByRole('button', { name: 'Complete Transaction' }))
    expect(await screen.findByText('You are not allowed to ring up sales.')).toBeTruthy()
    expect(usePOSStore.getState().items).toHaveLength(1)
  })

  it('shows the generic failure and keeps the lines for any other error', async () => {
    answer({})
    mockPost.mockRejectedValue(new Error('500'))
    chooseTerminal()
    seed()
    open()
    fireEvent.press(screen.getByRole('button', { name: 'Complete Transaction' }))
    expect(await screen.findByText('Failed to complete transaction.')).toBeTruthy()
    expect(usePOSStore.getState().items).toHaveLength(1)
  })

  it('forgets the terminal when the user signs out', () => {
    chooseTerminal()
    useAuthStore.getState().logout()
    expect(useTerminalStore.getState().terminalId).toBeNull()
  })
})
