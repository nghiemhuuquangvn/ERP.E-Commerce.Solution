import type { ReactNode } from 'react'
import { Text, View } from 'react-native'

/** Nothing to show yet: say why, and offer the next step. */
export function EmptyState({ icon, title, hint, action }: { icon?: ReactNode; title: string; hint?: string; action?: ReactNode }) {
  return (
    <View className="items-center px-6 py-12">
      {icon ? (
        <View className="mb-3" accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
          {icon}
        </View>
      ) : null}
      <Text accessibilityRole="header" className="text-center text-lg font-semibold text-ink">{title}</Text>
      {hint ? <Text className="mt-1 text-center text-sm text-ink-secondary">{hint}</Text> : null}
      {action ? <View className="mt-4 w-full max-w-xs">{action}</View> : null}
    </View>
  )
}
