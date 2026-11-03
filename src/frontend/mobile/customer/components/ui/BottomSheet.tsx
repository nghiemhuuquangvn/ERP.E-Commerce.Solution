import type { ReactNode } from 'react'
import { Modal, Pressable, Text, View } from 'react-native'

interface Props {
  visible: boolean
  title: string
  onClose: () => void
  closeLabel: string
  children: ReactNode
}

/** A panel from the bottom for one short task (adjust a quantity). The back button and a tap outside close it; nothing is lost on close. */
export function BottomSheet({ visible, title, onClose, closeLabel, children }: Props) {
  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose} accessibilityViewIsModal>
      <Pressable onPress={onClose} accessibilityLabel={closeLabel} accessibilityRole="button" className="flex-1 bg-ink/50" />
      <View className="rounded-t-sheet bg-surface px-4 pb-8 pt-4">
        <View className="mb-3 flex-row items-center justify-between">
          <Text accessibilityRole="header" className="flex-1 text-lg font-semibold text-ink">{title}</Text>
          <Pressable onPress={onClose} accessibilityRole="button" accessibilityLabel={closeLabel} className="h-11 w-11 items-center justify-center">
            <Text className="text-xl text-ink-secondary">✕</Text>
          </Pressable>
        </View>
        {children}
      </View>
    </Modal>
  )
}
