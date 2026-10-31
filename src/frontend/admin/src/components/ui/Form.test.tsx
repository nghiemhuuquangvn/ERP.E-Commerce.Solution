import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useForm } from 'react-hook-form'
import { axe } from '@/test/axe'
import { FormField } from './FormField'
import { Input, Textarea, Select, Checkbox } from './Input'
import { Switch } from './Switch'

describe('FormField', () => {
  it('links the label, the control and the hint', () => {
    render(<FormField label="Name" hint="Shown on receipts"><Input /></FormField>)
    const input = screen.getByLabelText('Name')
    expect(input).toHaveAttribute('aria-describedby')
    expect(screen.getByText('Shown on receipts')).toHaveAttribute('id', input.getAttribute('aria-describedby')!)
    expect(input).not.toHaveAttribute('aria-invalid')
  })

  it('shows the error instead of the hint, marks the control invalid and announces it', () => {
    render(<FormField label="Name" hint="Shown on receipts" error="Required"><Input /></FormField>)
    const input = screen.getByLabelText('Name')
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(screen.queryByText('Shown on receipts')).toBeNull()
    expect(screen.getByRole('alert')).toHaveTextContent('Required')
    expect(screen.getByRole('alert')).toHaveAttribute('id', input.getAttribute('aria-describedby')!)
    expect(input.className).toContain('border-danger')
  })

  it('marks required fields for assistive tech and sighted users', () => {
    render(<FormField label="Name" required><Input /></FormField>)
    expect(screen.getByLabelText(/Name/)).toHaveAttribute('aria-required', 'true')
    expect(screen.getByText('*')).toBeInTheDocument()
  })

  it('works for Textarea and Select too', () => {
    render(
      <div>
        <FormField label="Note" error="Too long"><Textarea /></FormField>
        <FormField label="Kind"><Select><option value="a">A</option></Select></FormField>
      </div>,
    )
    expect(screen.getByLabelText('Note')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Kind')).toBeInTheDocument()
  })

  it('a standalone control can be marked invalid with error', () => {
    render(<Input aria-label="x" error />)
    expect(screen.getByLabelText('x')).toHaveAttribute('aria-invalid', 'true')
  })

  it('forwards refs so react-hook-form register keeps working', async () => {
    const onValid = vi.fn()
    function Demo() {
      const { register, handleSubmit, formState: { errors } } = useForm<{ name: string }>()
      return (
        <form onSubmit={handleSubmit(onValid)}>
          <FormField label="Name" error={errors.name ? 'Required' : undefined}>
            <Input {...register('name', { required: true })} />
          </FormField>
          <button type="submit">Send</button>
        </form>
      )
    }
    render(<Demo />)
    await userEvent.click(screen.getByRole('button', { name: 'Send' }))
    expect(onValid).not.toHaveBeenCalled()
    expect(await screen.findByRole('alert')).toHaveTextContent('Required')
    await userEvent.type(screen.getByLabelText('Name'), 'Lan')
    await userEvent.click(screen.getByRole('button', { name: 'Send' }))
    expect(onValid).toHaveBeenCalledTimes(1)
  })

  it('has no axe violations (default and error)', async () => {
    const { container } = render(
      <form>
        <FormField label="Name" hint="Hint"><Input /></FormField>
        <FormField label="Code" error="Required" required><Input /></FormField>
        <Checkbox label="Active" />
        <Switch checked label="Visible" onChange={() => {}} />
      </form>,
    )
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Checkbox and Switch', () => {
  it('Checkbox is toggled by clicking its label', async () => {
    const onChange = vi.fn()
    render(<Checkbox label="Active" onChange={onChange} />)
    await userEvent.click(screen.getByText('Active'))
    expect(onChange).toHaveBeenCalledTimes(1)
  })

  it('Switch reports the new value on click and with Space / Enter, and exposes its state', async () => {
    const onChange = vi.fn()
    const { rerender } = render(<Switch checked={false} label="Visible" onChange={onChange} />)
    const sw = screen.getByRole('switch', { name: 'Visible' })
    expect(sw).toHaveAttribute('aria-checked', 'false')
    await userEvent.click(sw)
    expect(onChange).toHaveBeenLastCalledWith(true)
    await userEvent.keyboard(' ')
    await userEvent.keyboard('{Enter}')
    expect(onChange).toHaveBeenCalledTimes(3)
    rerender(<Switch checked label="Visible" onChange={onChange} />)
    expect(screen.getByRole('switch')).toHaveAttribute('aria-checked', 'true')
  })

  it('a disabled Switch does nothing', async () => {
    const onChange = vi.fn()
    render(<Switch checked={false} disabled label="Visible" onChange={onChange} />)
    await userEvent.click(screen.getByRole('switch'))
    expect(onChange).not.toHaveBeenCalled()
  })
})
