import { useEffect } from 'react'
import { Text, View } from 'react-native'
import { create } from 'zustand'

type Tone = 'success' | 'info' | 'danger'
interface ToastItem { id: number; tone: Tone; message: string }
interface ToastState {
  items: ToastItem[]
  push: (tone: Tone, message: string) => void
  dismiss: (id: number) => void
}

let nextId = 1
export const useToastStore = create<ToastState>()((set) => ({
  items: [],
  push: (tone, message) => set((s) => ({ items: [...s.items, { id: nextId++, tone, message }] })),
  dismiss: (id) => set((s) => ({ items: s.items.filter((i) => i.id !== id) })),
}))

/** Tell the user something happened without stopping them. Success and info vanish after 4 s; an error stays until dismissed by the next screen change or toast. */
export function useToast() {
  const push = useToastStore((s) => s.push)
  return {
    success: (message: string) => push('success', message),
    info: (message: string) => push('info', message),
    error: (message: string) => push('danger', message),
  }
}

const TONE: Record<Tone, string> = {
  success: 'bg-success',
  info: 'bg-ink',
  danger: 'bg-danger',
}

function ToastRow({ item }: { item: ToastItem }) {
  const dismiss = useToastStore((s) => s.dismiss)
  useEffect(() => {
    if (item.tone === 'danger') return
    const timer = setTimeout(() => dismiss(item.id), 4000)
    return () => clearTimeout(timer)
  }, [item, dismiss])
  return (
    <View
      accessible
      accessibilityRole="alert"
      accessibilityLiveRegion={item.tone === 'danger' ? 'assertive' : 'polite'}
      className={`mb-2 rounded-control px-4 py-3 ${TONE[item.tone]}`}
    >
      <Text className="text-sm font-medium text-on-brand">{item.message}</Text>
    </View>
  )
}

/** Mount once near the root. `bottom` keeps the toasts above the tab bar. */
export function ToastHost({ bottom = 72 }: { bottom?: number }) {
  const items = useToastStore((s) => s.items)
  if (items.length === 0) return null
  return (
    <View pointerEvents="box-none" style={{ position: 'absolute', left: 16, right: 16, bottom }}>
      {items.map((i) => <ToastRow key={i.id} item={i} />)}
    </View>
  )
}
