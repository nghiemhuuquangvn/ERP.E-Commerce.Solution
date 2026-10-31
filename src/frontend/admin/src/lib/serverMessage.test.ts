import { describe, it, expect } from 'vitest'
import { serverMessage } from './serverMessage'

const failure = (data: unknown) => ({ response: { status: 400, data } })

describe('serverMessage', () => {
  it('prefers the ProblemDetails detail', () => {
    expect(serverMessage(failure({ detail: 'Code already exists', message: 'other' }), 'fallback')).toBe('Code already exists')
  })
  it('falls back to message, then to the fallback', () => {
    expect(serverMessage(failure({ message: 'Old style' }), 'fallback')).toBe('Old style')
    expect(serverMessage(failure({}), 'fallback')).toBe('fallback')
  })
  it('ignores a plain-text body and a request that got no answer', () => {
    expect(serverMessage(failure('<html>502</html>'), 'fallback')).toBe('fallback')
    expect(serverMessage(new Error('Network Error'), 'fallback')).toBe('fallback')
    expect(serverMessage(null, 'fallback')).toBe('fallback')
    expect(serverMessage(undefined, 'fallback')).toBe('fallback')
  })
})
