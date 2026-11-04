import { Pressable, Text, View } from 'react-native'

export interface Segment<T extends string> {
  value: T
  label: string
}

/** Two to four exclusive views of one screen (7 / 30 / 90 days; Employees | Leave | Attendance). */
export function SegmentedControl<T extends string>({ segments, value, onChange }: { segments: Segment<T>[]; value: T; onChange: (v: T) => void }) {
  return (
    <View accessibilityRole="tablist" className="flex-row gap-0.5 rounded-control bg-surface-muted p-1">
      {segments.map((s) => {
        const on = s.value === value
        return (
          <Pressable
            key={s.value}
            onPress={() => onChange(s.value)}
            accessibilityRole="tab"
            accessibilityState={{ selected: on }}
            accessibilityLabel={s.label}
            className={`min-h-[44px] flex-1 items-center justify-center rounded-control ${on ? 'bg-surface' : ''}`}
          >
            <Text className={`text-sm ${on ? 'font-semibold text-ink' : 'text-ink-secondary'}`}>{s.label}</Text>
          </Pressable>
        )
      })}
    </View>
  )
}
