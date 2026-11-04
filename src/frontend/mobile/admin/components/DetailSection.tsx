import type { ReactNode } from 'react'
import { Text, View } from 'react-native'
import { Card } from '@/components/ui'

/** A titled card on a detail screen: the title is a heading for a screen reader, the body is whatever the section holds. */
export function DetailSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card>
      <Text accessibilityRole="header" className="mb-2 text-base font-semibold text-ink">{title}</Text>
      <View className="gap-1.5">{children}</View>
    </Card>
  )
}

/** The big line at the top of a detail screen: the record's name or number on the left, its status badge on the right. */
export function DetailHeader({ title, badge }: { title: string; badge?: ReactNode }) {
  return (
    <View className="flex-row items-center justify-between gap-2">
      <Text accessibilityRole="header" className="flex-1 text-xl font-semibold text-ink">{title}</Text>
      {badge}
    </View>
  )
}
