import { act, fireEvent, render, screen } from '@testing-library/react-native'
import { Text } from 'react-native'
import { Alert, Badge, BottomSheet, Button, Card, EmptyState, QuantityStepper, Skeleton, TextField, ToastHost, useToast } from './index'
import { useToastStore } from './Toast'

describe('Button', () => {
  it('is a named button and calls onPress', () => {
    const onPress = jest.fn()
    render(<Button onPress={onPress}>Pay</Button>)
    fireEvent.press(screen.getByRole('button', { name: 'Pay' }))
    expect(onPress).toHaveBeenCalledTimes(1)
  })

  it('does nothing while loading and says it is busy', () => {
    const onPress = jest.fn()
    render(<Button onPress={onPress} loading>Pay</Button>)
    const button = screen.getByRole('button')
    fireEvent.press(button)
    expect(onPress).not.toHaveBeenCalled()
    expect(button.props.accessibilityState).toMatchObject({ busy: true, disabled: true })
  })

  it('does nothing when disabled', () => {
    const onPress = jest.fn()
    render(<Button onPress={onPress} disabled>Pay</Button>)
    fireEvent.press(screen.getByRole('button'))
    expect(onPress).not.toHaveBeenCalled()
  })

  it('can carry a longer accessible name than its text', () => {
    render(<Button accessibilityLabel="Remove Blue shirt" variant="outline">Remove</Button>)
    expect(screen.getByRole('button', { name: 'Remove Blue shirt' })).toBeTruthy()
  })
})

describe('TextField', () => {
  it('is named by its label, not the placeholder', () => {
    render(<TextField label="Email" placeholder="you@example.com" />)
    expect(screen.getByLabelText('Email')).toBeTruthy()
  })

  it('marks a required field and reads its error as an alert', () => {
    render(<TextField label="Email" required error="Enter an email" />)
    const input = screen.getByLabelText('Email *')
    expect(input.props['aria-invalid']).toBe(true)
    expect(input.props.accessibilityHint).toBe('Enter an email')
    expect(screen.getByRole('alert')).toHaveTextContent('Enter an email')
  })

  it('shows the hint when there is no error', () => {
    render(<TextField label="Password" hint="At least 8 characters" />)
    expect(screen.getByText('At least 8 characters')).toBeTruthy()
  })
})

describe('Alert, Badge, Card, EmptyState, Skeleton', () => {
  it('Alert is an alert region', () => {
    render(<Alert tone="danger">Wrong password</Alert>)
    expect(screen.getByRole('alert')).toHaveTextContent('Wrong password')
  })

  it('Badge shows its word', () => {
    render(<Badge tone="success" label="Paid" />)
    expect(screen.getByText('Paid')).toBeTruthy()
  })

  it('Card with onPress is one button; without it is plain', () => {
    const onPress = jest.fn()
    const { rerender } = render(<Card onPress={onPress} accessibilityLabel="Order 1"><Text>Order 1</Text></Card>)
    fireEvent.press(screen.getByRole('button', { name: 'Order 1' }))
    expect(onPress).toHaveBeenCalled()
    rerender(<Card><Text>Order 1</Text></Card>)
    expect(screen.queryByRole('button')).toBeNull()
  })

  it('EmptyState has a heading, a hint and an action', () => {
    render(<EmptyState title="Your cart is empty" hint="Add something" action={<Button>Browse</Button>} />)
    expect(screen.getByRole('header')).toHaveTextContent('Your cart is empty')
    expect(screen.getByRole('button', { name: 'Browse' })).toBeTruthy()
  })

  it('Skeleton is hidden from screen readers', () => {
    const { toJSON } = render(<Skeleton />)
    expect((toJSON() as { props: Record<string, unknown> }).props.accessibilityElementsHidden).toBe(true)
  })
})

describe('QuantityStepper', () => {
  it('names both buttons after the product and steps by one', () => {
    const onChange = jest.fn()
    render(<QuantityStepper value={2} onChange={onChange} name="Blue shirt" min={1} decreaseLabel="Fewer" increaseLabel="More" />)
    fireEvent.press(screen.getByRole('button', { name: 'More Blue shirt' }))
    fireEvent.press(screen.getByRole('button', { name: 'Fewer Blue shirt' }))
    expect(onChange).toHaveBeenNthCalledWith(1, 3)
    expect(onChange).toHaveBeenNthCalledWith(2, 1)
  })

  it('cannot go below min or above max', () => {
    const onChange = jest.fn()
    render(<QuantityStepper value={1} onChange={onChange} name="X" min={1} max={1} decreaseLabel="Fewer" increaseLabel="More" />)
    fireEvent.press(screen.getByRole('button', { name: 'Fewer X' }))
    fireEvent.press(screen.getByRole('button', { name: 'More X' }))
    expect(onChange).not.toHaveBeenCalled()
  })
})

describe('Toast', () => {
  beforeEach(() => {
    jest.useFakeTimers()
    useToastStore.setState({ items: [] })
  })
  afterEach(() => jest.useRealTimers())

  function Trigger({ kind }: { kind: 'success' | 'error' }) {
    const toast = useToast()
    return <Button onPress={() => toast[kind]('Saved')}>go</Button>
  }

  it('shows a success toast and removes it after 4 seconds', () => {
    render(<><Trigger kind="success" /><ToastHost /></>)
    fireEvent.press(screen.getByRole('button'))
    expect(screen.getByText('Saved')).toBeTruthy()
    act(() => { jest.advanceTimersByTime(4100) })
    expect(screen.queryByText('Saved')).toBeNull()
  })

  it('keeps an error until it is replaced', () => {
    render(<><Trigger kind="error" /><ToastHost /></>)
    fireEvent.press(screen.getByRole('button'))
    act(() => { jest.advanceTimersByTime(10000) })
    expect(screen.getByText('Saved')).toBeTruthy()
  })
})

describe('BottomSheet', () => {
  it('shows its title and closes from the named close button', () => {
    const onClose = jest.fn()
    render(<BottomSheet visible title="Adjust stock" onClose={onClose} closeLabel="Close"><Text>body</Text></BottomSheet>)
    expect(screen.getByRole('header')).toHaveTextContent('Adjust stock')
    fireEvent.press(screen.getAllByRole('button', { name: 'Close' })[0])
    expect(onClose).toHaveBeenCalled()
  })

  it('renders nothing while hidden', () => {
    render(<BottomSheet visible={false} title="Adjust stock" onClose={() => {}} closeLabel="Close"><Text>body</Text></BottomSheet>)
    expect(screen.queryByText('body')).toBeNull()
  })
})
