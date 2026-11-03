import { Pressable, Text, View } from 'react-native'

interface Props {
  value: number
  onChange: (next: number) => void
  /** What is counted, used in the names: "Quantity of Blue shirt". */
  name: string
  min?: number
  max?: number
  decreaseLabel: string
  increaseLabel: string
}

/** − value +. Both buttons are 44 pt and named; the group is announced as an adjustable value. */
export function QuantityStepper({ value, onChange, name, min = 0, max, decreaseLabel, increaseLabel }: Props) {
  const canDecrease = value > min
  const canIncrease = max === undefined || value < max
  return (
    <View className="flex-row items-center gap-1" accessibilityLabel={name} accessibilityValue={{ now: value, min, max }}>
      <Pressable
        onPress={canDecrease ? () => onChange(value - 1) : undefined}
        accessibilityRole="button"
        accessibilityLabel={`${decreaseLabel} ${name}`}
        accessibilityState={{ disabled: !canDecrease }}
        className={`h-11 w-11 items-center justify-center rounded-full bg-surface-muted ${canDecrease ? '' : 'opacity-50'}`}
      >
        <Text className="text-lg text-ink">−</Text>
      </Pressable>
      <Text className="min-w-[28px] text-center text-base font-medium text-ink">{value}</Text>
      <Pressable
        onPress={canIncrease ? () => onChange(value + 1) : undefined}
        accessibilityRole="button"
        accessibilityLabel={`${increaseLabel} ${name}`}
        accessibilityState={{ disabled: !canIncrease }}
        className={`h-11 w-11 items-center justify-center rounded-full bg-surface-muted ${canIncrease ? '' : 'opacity-50'}`}
      >
        <Text className="text-lg text-ink">+</Text>
      </Pressable>
    </View>
  )
}
