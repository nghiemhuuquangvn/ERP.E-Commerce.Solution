import { Text, View } from 'react-native'

/** One order line: name and quantity on the left, the amount on the right. */
export function LineItemRow({ name, detail, amount, strong }: { name: string; detail?: string; amount: string; strong?: boolean }) {
  const weight = strong ? 'font-semibold' : ''
  return (
    <View accessible accessibilityLabel={`${name}${detail ? `, ${detail}` : ''}, ${amount}`} className="flex-row justify-between gap-3 border-t border-line py-2">
      <View className="flex-1">
        <Text className={`text-sm ${weight} text-ink`}>{name}</Text>
        {detail ? <Text className="text-xs text-ink-muted">{detail}</Text> : null}
      </View>
      <Text className={`text-sm ${weight} text-ink`}>{amount}</Text>
    </View>
  )
}
