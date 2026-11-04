import { Pressable } from 'react-native'
import { useTranslation } from 'react-i18next'
import { Menu } from 'lucide-react-native'
import { useMenuStore } from '@/stores/menu.store'
import { colors, icon } from '@/theme/theme'

/** Header button that opens the side menu. */
export function MenuButton() {
  const { t } = useTranslation()
  const setOpen = useMenuStore((s) => s.setOpen)
  return (
    <Pressable onPress={() => setOpen(true)} accessibilityRole="button" accessibilityLabel={t('header.openMenu')} className="h-11 w-11 items-center justify-center">
      <Menu color={colors.ink} size={24} strokeWidth={icon.stroke} />
    </Pressable>
  )
}
