import type { LucideIcon } from 'lucide-react-native'

/** A line of "Needs your action" on Home: how many things wait for this user, and where to open them. */
export interface Attention {
  key: string
  /** i18n key with a {{count}} placeholder. */
  labelKey: string
  permission: string
  count: () => Promise<number>
  href: { pathname: string; params?: Record<string, string> }
}

/**
 * One back-office module of the app. A module is a folder under modules/ that exports one of these; adding it to
 * modules/index.ts is all the navigation needs. Its screen is the file app/(tabs)/<route>.tsx (expo-router is file based);
 * a test fails when the file or a translation is missing.
 */
export interface ErpModule {
  id: string
  /** i18n key of the tab / list title. */
  titleKey: string
  icon: LucideIcon
  /** File name (without extension) of the tab screen in app/(tabs). */
  route: string
  /** The user needs ANY of these permissions to see the module; empty = every signed-in user. */
  permissions: string[]
  /** Licensed module code (SO, PO, HRM ...): when the token lists licensed modules, this one must be among them. */
  licensedModule?: string
  /** Position in the tab bar (low first). */
  order: number
  attention?: Attention[]
}
