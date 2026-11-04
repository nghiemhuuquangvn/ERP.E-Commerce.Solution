import { Text, View } from 'react-native'
import { Card } from '@/components/ui'

/** One figure with its name; read together by a screen reader. */
export function KpiCard({ label, value }: { label: string; value: string }) {
  return (
    <View className="flex-1">
      <Card>
        <Text className="mb-1 text-sm text-ink-secondary">{label}</Text>
        <Text accessibilityLabel={`${label}: ${value}`} className="text-2xl font-bold text-ink">{value}</Text>
      </Card>
    </View>
  )
}
