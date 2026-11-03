import { Text, View } from 'react-native'

type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'brand'

const TONE: Record<Tone, { box: string; text: string }> = {
  neutral: { box: 'bg-muted-soft', text: 'text-muted-soft-text' },
  info: { box: 'bg-info-soft', text: 'text-info-soft-text' },
  success: { box: 'bg-success-soft', text: 'text-success-soft-text' },
  warning: { box: 'bg-warning-soft', text: 'text-warning-soft-text' },
  danger: { box: 'bg-danger-soft', text: 'text-danger-soft-text' },
  brand: { box: 'bg-brand-soft', text: 'text-brand-soft-text' },
}

/** A status chip. The word carries the meaning; the colour only backs it up. */
export function Badge({ tone = 'neutral', label }: { tone?: Tone; label: string }) {
  return (
    <View className={`self-start rounded-full px-2.5 py-1 ${TONE[tone].box}`}>
      <Text className={`text-sm font-medium ${TONE[tone].text}`}>{label}</Text>
    </View>
  )
}
