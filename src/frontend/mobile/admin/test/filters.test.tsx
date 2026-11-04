import { Pressable } from 'react-native'
import { act, fireEvent, render, renderHook, screen } from '@testing-library/react-native'
import '@/i18n'
import i18n from '@/i18n'
import { DateField } from '@/components/DateField'
import { ListFilters } from '@/components/ListFilters'
import { SEARCH_DEBOUNCE_MS, useListFilters } from '@/hooks/useListFilters'

const NOW = new Date('2026-11-04T10:00:00')

beforeEach(() => {
  void i18n.changeLanguage('en')
})

describe('useListFilters', () => {
  it('starts with nothing to send', () => {
    const { result } = renderHook(() => useListFilters({ now: NOW }))
    expect(result.current.params).toEqual({})
    expect(result.current.activeCount).toBe(0)
  })

  it('opens on a preset status and counts it as an active filter', () => {
    const { result } = renderHook(() => useListFilters({ now: NOW, status: 'SUBMITTED' }))
    expect(result.current.params).toEqual({ status: 'SUBMITTED' })
    expect(result.current.activeCount).toBe(1)
  })

  it('a month, a quarter and a year become a from-to pair, and can be stepped', () => {
    const { result } = renderHook(() => useListFilters({ now: NOW }))
    act(() => result.current.setKind('month'))
    expect(result.current.params).toEqual({ dateFrom: '2026-11-01', dateTo: '2026-11-30' })
    expect(result.current.periodText).toBe('2026-11')
    act(() => result.current.step(-1))
    expect(result.current.params).toEqual({ dateFrom: '2026-10-01', dateTo: '2026-10-31' })
    act(() => result.current.setKind('quarter'))
    expect(result.current.params).toEqual({ dateFrom: '2026-10-01', dateTo: '2026-12-31' })
    expect(result.current.periodText).toBe('Q4 2026')
    act(() => result.current.step(1))
    expect(result.current.periodText).toBe('Q1 2027')
    act(() => result.current.setKind('year'))
    expect(result.current.params).toEqual({ dateFrom: '2026-01-01', dateTo: '2026-12-31' })
  })

  it('switching to another period starts from today again', () => {
    const { result } = renderHook(() => useListFilters({ now: NOW }))
    act(() => result.current.setKind('month'))
    act(() => result.current.step(-3))
    act(() => result.current.setKind('quarter'))
    expect(result.current.periodText).toBe('Q4 2026')
  })

  it('a custom range sends the typed days; one end alone is allowed', () => {
    const { result } = renderHook(() => useListFilters({ now: NOW }))
    act(() => result.current.setKind('custom'))
    expect(result.current.params).toEqual({})
    act(() => result.current.setFrom('2026-11-10'))
    expect(result.current.params).toEqual({ dateFrom: '2026-11-10' })
    act(() => result.current.setTo('2026-11-20'))
    expect(result.current.params).toEqual({ dateFrom: '2026-11-10', dateTo: '2026-11-20' })
    expect(result.current.periodText).toBe('2026-11-10 → 2026-11-20')
  })

  it('a range that ends before it starts is not sent and is flagged', () => {
    const { result } = renderHook(() => useListFilters({ now: NOW }))
    act(() => result.current.setKind('custom'))
    act(() => { result.current.setFrom('2026-12-01'); result.current.setTo('2026-11-01') })
    expect(result.current.rangeError).toBe(true)
    expect(result.current.params).toEqual({})
  })

  it('a text that is not a real day is ignored', () => {
    const { result } = renderHook(() => useListFilters({ now: NOW }))
    act(() => result.current.setKind('custom'))
    act(() => result.current.setFrom('2026-02-30'))
    expect(result.current.params).toEqual({})
  })

  it('the search text is sent only after the typing pauses', () => {
    jest.useFakeTimers()
    try {
      const { result } = renderHook(() => useListFilters({ now: NOW }))
      act(() => result.current.setSearch('  PO-1'))
      act(() => { jest.advanceTimersByTime(SEARCH_DEBOUNCE_MS - 50) })
      expect(result.current.params.search).toBeUndefined()
      act(() => result.current.setSearch('PO-12'))
      act(() => { jest.advanceTimersByTime(SEARCH_DEBOUNCE_MS - 50) })
      expect(result.current.params.search).toBeUndefined()
      act(() => { jest.advanceTimersByTime(100) })
      expect(result.current.params.search).toBe('PO-12')
    } finally {
      jest.useRealTimers()
    }
  })

  it('reset brings back the opening filters', () => {
    const { result } = renderHook(() => useListFilters({ now: NOW, status: 'Pending' }))
    act(() => { result.current.setStatus('Approved'); result.current.setKind('month') })
    expect(result.current.activeCount).toBe(2)
    act(() => result.current.reset())
    expect(result.current.params).toEqual({ status: 'Pending' })
    expect(result.current.kind).toBe('all')
    expect(result.current.search).toBe('')
  })
})

function Harness({ kind = 'all' as const, statuses = true, noDates = false }: { kind?: 'all' | 'custom'; statuses?: boolean; noDates?: boolean }) {
  const filters = useListFilters({ now: NOW, kind })
  return (
    <>
      <ListFilters
        filters={filters}
        searchLabel="Search orders"
        noDates={noDates}
        statuses={statuses ? [{ value: 'SUBMITTED', label: 'Submitted' }, { value: 'APPROVED', label: 'Approved' }] : undefined}
      />
      <Pressable accessibilityLabel="params" accessibilityValue={{ text: JSON.stringify(filters.params) }} />
      <Pressable accessibilityLabel="bad-range" onPress={() => { filters.setFrom('2026-12-01'); filters.setTo('2026-11-01') }} />
    </>
  )
}

const params = () => JSON.parse(screen.getByLabelText('params').props.accessibilityValue.text)

describe('the filters above a list', () => {
  it('shows the search box and a Filters button without a number while nothing is filtered', () => {
    render(<Harness />)
    expect(screen.getByLabelText('Search orders')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Open filters' })).toBeTruthy()
  })

  it('has no Filters button for a list with neither status nor dates', () => {
    render(<Harness statuses={false} noDates />)
    expect(screen.queryByRole('button', { name: 'Open filters' })).toBeNull()
    expect(screen.getByLabelText('Search orders')).toBeTruthy()
  })

  it('a status chosen in the sheet filters the list and shows on the summary line, which clears it again', () => {
    render(<Harness />)
    fireEvent.press(screen.getByRole('button', { name: 'Open filters' }))
    fireEvent.press(screen.getByRole('tab', { name: 'Approved' }))
    expect(params()).toEqual({ status: 'APPROVED' })
    fireEvent.press(screen.getByRole('button', { name: 'Done' }))
    expect(screen.getByRole('button', { name: 'Open filters, 1 active' })).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Clear filter Approved' }))
    expect(params()).toEqual({})
  })

  it('a period is picked and stepped in the sheet', () => {
    render(<Harness />)
    fireEvent.press(screen.getByRole('button', { name: 'Open filters' }))
    fireEvent.press(screen.getByRole('tab', { name: 'Month' }))
    expect(params()).toEqual({ dateFrom: '2026-11-01', dateTo: '2026-11-30' })
    fireEvent.press(screen.getByRole('button', { name: 'Previous period' }))
    expect(params()).toEqual({ dateFrom: '2026-10-01', dateTo: '2026-10-31' })
    fireEvent.press(screen.getByRole('tab', { name: 'Quarter' }))
    expect(screen.getAllByText('Q4 2026').length).toBeGreaterThan(0)
    fireEvent.press(screen.getByRole('tab', { name: 'All time' }))
    expect(params()).toEqual({})
  })

  it('a custom range shows two date boxes and refuses an end before the start', () => {
    render(<Harness kind="custom" />)
    fireEvent.press(screen.getByRole('button', { name: 'Open filters' }))
    expect(screen.getByRole('button', { name: 'From date: Choose a date' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'To date: Choose a date' })).toBeTruthy()
    fireEvent.press(screen.getByLabelText('bad-range'))
    expect(screen.getByText('The start date is after the end date.')).toBeTruthy()
    expect(params()).toEqual({})
  })

  it('Reset clears what the sheet holds', () => {
    render(<Harness />)
    fireEvent.press(screen.getByRole('button', { name: 'Open filters' }))
    fireEvent.press(screen.getByRole('tab', { name: 'Approved' }))
    fireEvent.press(screen.getByRole('tab', { name: 'Month' }))
    fireEvent.press(screen.getByRole('button', { name: 'Reset' }))
    expect(params()).toEqual({})
  })
})

describe('the date box', () => {
  it('shows the chosen day, opens a month grid and returns the day tapped', () => {
    const onChange = jest.fn()
    render(<DateField label="From date" value="2026-11-04" onChange={onChange} />)
    fireEvent.press(screen.getByRole('button', { name: 'From date: 2026-11-04' }))
    expect(screen.getByLabelText('2026-11-04').props.accessibilityState.selected).toBe(true)
    fireEvent.press(screen.getByLabelText('2026-11-20'))
    expect(onChange).toHaveBeenCalledWith('2026-11-20')
  })

  it('moves between months', () => {
    const onChange = jest.fn()
    render(<DateField label="To date" value="2026-11-04" onChange={onChange} />)
    fireEvent.press(screen.getByRole('button', { name: 'To date: 2026-11-04' }))
    fireEvent.press(screen.getByRole('button', { name: 'Next month' }))
    fireEvent.press(screen.getByLabelText('2026-12-15'))
    expect(onChange).toHaveBeenCalledWith('2026-12-15')
  })

  it('opens on the fallback month when there is no value, and can clear or pick today', () => {
    const onChange = jest.fn()
    render(<DateField label="To date" value="" fallback="2026-03-10" onChange={onChange} />)
    fireEvent.press(screen.getByRole('button', { name: 'To date: Choose a date' }))
    expect(screen.getByLabelText('2026-03-10')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Clear date' }))
    expect(onChange).toHaveBeenCalledWith('')
    fireEvent.press(screen.getByRole('button', { name: 'To date: Choose a date' }))
    fireEvent.press(screen.getByRole('button', { name: 'Today' }))
    expect(onChange).toHaveBeenLastCalledWith(expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/))
  })
})
