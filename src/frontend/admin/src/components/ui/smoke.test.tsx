import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { axe } from '@/test/axe'

describe('test framework', () => {
  it('renders, translates and runs axe', async () => {
    const { container } = render(<button type="button">Save</button>)
    expect(screen.getByRole('button', { name: 'Save' })).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })
})
