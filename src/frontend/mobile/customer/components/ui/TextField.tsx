import { Text, TextInput, View, type TextInputProps } from 'react-native'
import { colors } from '@/theme/theme'

interface Props extends Omit<TextInputProps, 'style'> {
  label: string
  hint?: string
  error?: string
  required?: boolean
}

/** A labelled text box. The label is the accessible name (a placeholder is only an example); the error is read with the field and marks it invalid. */
export function TextField({ label, hint, error, required, ...input }: Props) {
  return (
    <View className="mb-3">
      <Text className="mb-1 text-sm font-medium text-ink">
        {label}
        {required ? <Text className="text-danger"> *</Text> : null}
      </Text>
      <TextInput
        accessibilityLabel={required ? `${label} *` : label}
        accessibilityHint={error ?? hint}
        aria-invalid={!!error}
        placeholderTextColor={colors.inkMuted}
        className={`min-h-[48px] rounded-control border bg-surface px-4 py-3 text-base text-ink ${error ? 'border-danger' : 'border-line-strong'}`}
        {...input}
      />
      {error ? (
        <Text accessibilityRole="alert" className="mt-1 text-sm text-danger">
          {error}
        </Text>
      ) : hint ? (
        <Text className="mt-1 text-sm text-ink-secondary">{hint}</Text>
      ) : null}
    </View>
  )
}
