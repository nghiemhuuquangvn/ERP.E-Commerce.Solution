import type { ReactNode } from 'react'
import { Pressable, View } from 'react-native'

interface Props {
  children: ReactNode
  onPress?: () => void
  /** Name read for a card that can be opened. */
  accessibilityLabel?: string
}

/** A bordered surface; with onPress the whole card is one button (the target is the card, not a small link inside it). */
export function Card({ children, onPress, accessibilityLabel }: Props) {
  const cls = 'rounded-card border border-line bg-surface p-4'
  if (!onPress) return <View className={cls}>{children}</View>
  return (
    <Pressable onPress={onPress} accessibilityRole="button" accessibilityLabel={accessibilityLabel} className={`${cls} min-h-[48px]`}>
      {children}
    </Pressable>
  )
}
