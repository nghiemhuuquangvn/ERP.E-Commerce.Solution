import { forwardRef } from 'react'
import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { cx } from './cx'
import { Spinner } from './Spinner'

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'danger' | 'ghost' | 'link'
export type ButtonSize = 'sm' | 'md' | 'lg'

const VARIANT: Record<ButtonVariant, string> = {
  primary: 'bg-brand-action text-on-brand hover:bg-brand-action-hover',
  secondary: 'bg-surface-muted text-ink hover:bg-line',
  outline: 'border border-line-strong bg-surface text-ink hover:bg-surface-muted',
  danger: 'bg-danger text-on-brand hover:bg-danger-hover',
  ghost: 'text-ink-secondary hover:bg-surface-muted',
  link: 'text-brand-action underline-offset-2 hover:underline',
}
const SIZE: Record<ButtonSize, string> = { sm: 'h-8 text-sm', md: 'h-9 text-sm', lg: 'h-11 text-base' }
const PADDING: Record<ButtonSize, string> = { sm: 'px-3', md: 'px-4', lg: 'px-5' }
const ICON_ONLY: Record<ButtonSize, string> = { sm: 'w-8', md: 'w-9', lg: 'w-11' }

interface BaseProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'children'> {
  variant?: ButtonVariant
  size?: ButtonSize
  /** Shows a spinner in place of the label's width and ignores clicks. */
  loading?: boolean
  fullWidth?: boolean
  leftIcon?: ReactNode
  rightIcon?: ReactNode
}
type LabelProps = BaseProps & { iconOnly?: false; children: ReactNode }
/** An icon-only button has no text, so it must carry an accessible name. */
type IconOnlyProps = BaseProps & { iconOnly: true; 'aria-label': string; children: ReactNode }
export type ButtonProps = LabelProps | IconOnlyProps

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(props, ref) {
  const { variant = 'primary', size = 'md', loading = false, fullWidth, leftIcon, rightIcon, iconOnly, className, disabled, type = 'button', children, ...rest } = props
  const isLink = variant === 'link'
  return (
    <button
      ref={ref}
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={cx(
        'relative inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action focus-visible:ring-offset-2',
        'disabled:pointer-events-none disabled:opacity-50',
        VARIANT[variant],
        !isLink && SIZE[size],
        !isLink && (iconOnly ? ICON_ONLY[size] : PADDING[size]),
        fullWidth && 'w-full',
        className,
      )}
      {...rest}
    >
      {/* The label keeps its width while loading so the button does not jump. */}
      <span className={cx('inline-flex items-center justify-center gap-2', loading && 'invisible')}>
        {leftIcon}
        {children}
        {rightIcon}
      </span>
      {loading && (
        <span className="absolute inset-0 flex items-center justify-center">
          <Spinner />
        </span>
      )}
    </button>
  )
})
