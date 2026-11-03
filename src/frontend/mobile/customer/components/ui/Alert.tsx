import type { ReactNode } from 'react'
import { Text, View } from 'react-native'

type Tone = 'info' | 'success' | 'warning' | 'danger'

const TONE: Record<Tone, { box: string; text: string }> = {
  info: { box: 'bg-info-soft', text: 'text-info-soft-text' },
  success: { box: 'bg-success-soft', text: 'text-success-soft-text' },
  warning: { box: 'bg-warning-soft', text: 'text-warning-soft-text' },
  danger: { box: 'bg-danger-soft', text: 'text-danger-soft-text' },
}

/** An inline message. A danger alert is announced at once; the others wait for the reader. */
export function Alert({ tone = 'info', children }: { tone?: Tone; children: ReactNode }) {
  return (
    <View
      accessible
      accessibilityRole="alert"
      accessibilityLiveRegion={tone === 'danger' ? 'assertive' : 'polite'}
      className={`rounded-control px-4 py-3 ${TONE[tone].box}`}
    >
      <Text className={`text-sm ${TONE[tone].text}`}>{children}</Text>
    </View>
  )
}
