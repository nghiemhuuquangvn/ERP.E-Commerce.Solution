import type { ReactNode } from 'react'
import { ActivityIndicator, Pressable, Text } from 'react-native'
import { colors } from '@/theme/theme'

type Variant = 'primary' | 'outline' | 'ghost' | 'danger'
type Size = 'md' | 'lg'

interface Props {
  children: ReactNode
  onPress?: () => void
  variant?: Variant
  /** md = 48 pt (customer), lg = 52 pt (staff: gloves, one hand). */
  size?: Size
  loading?: boolean
  disabled?: boolean
  /** Name read by a screen reader when the visible text is not enough ("Remove Blue shirt"). */
  accessibilityLabel?: string
  fullWidth?: boolean
}

const BOX: Record<Variant, string> = {
  primary: 'bg-brand-action',
  outline: 'bg-surface border border-line-strong',
  ghost: 'bg-transparent',
  danger: 'bg-danger',
}
const TEXT: Record<Variant, string> = {
  primary: 'text-on-brand',
  outline: 'text-ink',
  ghost: 'text-brand-action',
  danger: 'text-on-brand',
}

export function Button({
  children,
  onPress,
  variant = 'primary',
  size = 'md',
  loading = false,
  disabled = false,
  accessibilityLabel,
  fullWidth = true,
}: Props) {
  const off = disabled || loading
  return (
    <Pressable
      onPress={off ? undefined : onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityState={{ disabled: off, busy: loading }}
      className={`flex-row items-center justify-center gap-2 rounded-control px-5 ${size === 'lg' ? 'min-h-[52px]' : 'min-h-[48px]'} ${BOX[variant]} ${fullWidth ? '' : 'self-start'} ${off ? 'opacity-50' : ''}`}
    >
      {loading && <ActivityIndicator color={variant === 'primary' || variant === 'danger' ? colors.onBrand : colors.brandAction} />}
      <Text className={`text-base font-semibold ${TEXT[variant]}`}>{children}</Text>
    </Pressable>
  )
}
