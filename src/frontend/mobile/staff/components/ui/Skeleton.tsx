import { View } from 'react-native'

/** A grey block that holds the place of content that is loading. Hidden from screen readers; the screen announces loading once. */
export function Skeleton({ className = 'h-4 w-full' }: { className?: string }) {
  return <View accessibilityElementsHidden importantForAccessibility="no-hide-descendants" className={`rounded-control bg-surface-muted ${className}`} />
}
