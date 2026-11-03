import { ActivityIndicator } from 'react-native'
import { colors } from '@/theme/theme'

/** Indeterminate progress. Decorative on its own: the surrounding region says what is loading. */
export function Spinner({ label }: { label?: string }) {
  return <ActivityIndicator color={colors.brandAction} accessibilityLabel={label} />
}
