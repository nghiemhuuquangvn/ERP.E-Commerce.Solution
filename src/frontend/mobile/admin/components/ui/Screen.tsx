import type { ReactNode } from 'react'
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'

interface Props {
  children: ReactNode
  /** false for a screen that scrolls a list itself (FlatList). */
  scroll?: boolean
  /** Centre a short form (sign in). */
  center?: boolean
  /** Edges to keep clear of the notch / gesture bar; tab screens leave top and bottom to the navigator. */
  edges?: ('top' | 'bottom' | 'left' | 'right')[]
}

/** Page background, safe area, keyboard avoidance and (optionally) scrolling in one place. */
export function Screen({ children, scroll = true, center = false, edges = ['left', 'right'] }: Props) {
  const body = scroll ? (
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName={center ? 'flex-grow justify-center px-6 py-8' : 'p-4'}>
      {children}
    </ScrollView>
  ) : (
    <View className="flex-1">{children}</View>
  )
  return (
    <SafeAreaView edges={edges} className="flex-1 bg-surface">
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} className="flex-1">
        {body}
      </KeyboardAvoidingView>
    </SafeAreaView>
  )
}
