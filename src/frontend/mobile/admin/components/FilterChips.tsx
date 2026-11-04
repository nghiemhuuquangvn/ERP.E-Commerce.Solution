import { Pressable, ScrollView, Text } from 'react-native'

export interface Chip<T extends string> {
  value: T
  label: string
}

/** A row of status filters; the chosen one is announced as selected. */
export function FilterChips<T extends string>({ chips, value, onChange }: { chips: Chip<T>[]; value: T; onChange: (v: T) => void }) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} accessibilityRole="tablist" contentContainerClassName="gap-2 py-1">
      {chips.map((c) => {
        const on = c.value === value
        return (
          <Pressable
            key={c.value}
            onPress={() => onChange(c.value)}
            accessibilityRole="tab"
            accessibilityState={{ selected: on }}
            accessibilityLabel={c.label}
            className={`min-h-[44px] justify-center rounded-full border px-4 ${on ? 'border-brand-action bg-brand-action' : 'border-line bg-surface'}`}
          >
            <Text className={`text-sm ${on ? 'font-semibold text-on-brand' : 'text-ink-secondary'}`}>{c.label}</Text>
          </Pressable>
        )
      })}
    </ScrollView>
  )
}
