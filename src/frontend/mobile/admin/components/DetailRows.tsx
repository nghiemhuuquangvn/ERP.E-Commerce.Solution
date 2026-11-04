import { Text, View } from 'react-native'

export interface DetailRow {
  label: string
  value: string | null | undefined
}

/** Label / value pairs of one record; each row is read as "Supplier: Acme Ltd". Empty values are left out. */
export function DetailRows({ rows }: { rows: DetailRow[] }) {
  return (
    <View className="gap-1.5">
      {rows
        .filter((r) => r.value)
        .map((r) => (
          <View key={r.label} accessible accessibilityLabel={`${r.label}: ${r.value}`} className="flex-row justify-between gap-4">
            <Text className="text-sm text-ink-muted">{r.label}</Text>
            <Text className="flex-1 text-right text-sm text-ink">{r.value}</Text>
          </View>
        ))}
    </View>
  )
}
