import { TextInput, View } from 'react-native'
import { Search } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'

/** The search box above a list. The caller debounces / submits; this only edits text. */
export function SearchField({ value, onChangeText, placeholder, label }: { value: string; onChangeText: (v: string) => void; placeholder: string; label: string }) {
  return (
    <View className="min-h-[44px] flex-row items-center gap-2 rounded-control border border-line-strong bg-surface px-3">
      <Search color={colors.inkMuted} size={18} strokeWidth={icon.stroke} />
      <TextInput
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={colors.inkMuted}
        accessibilityLabel={label}
        returnKeyType="search"
        autoCapitalize="none"
        autoCorrect={false}
        className="min-h-[44px] flex-1 text-base text-ink"
      />
    </View>
  )
}
