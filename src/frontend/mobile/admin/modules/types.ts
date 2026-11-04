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
 * One business function of a module (purchase orders, suppliers, leave requests ...). Inside a module the bottom bar shows its
 * sections. Its screen is the file app/(tabs)/<route>.tsx; a test fails when the file or a translation is missing.
 */
export interface ModuleSection {
  id: string
  /** i18n key of the bar label and the screen title. */
  titleKey: string
  icon: LucideIcon
  /** File name (without extension) of the screen in app/(tabs). */
  route: string
  /** The user needs ANY of these permissions to see the section; empty = every user who may see the module. */
  permissions: string[]
  /** Position in the bar (low first). */
  order: number
}

/**
 * One back-office module of the app. A module is a folder under modules/ that exports one of these; adding it to
 * modules/index.ts is all the navigation needs. Its screens are files in app/(tabs) (expo-router is file based); a test fails
 * when a file or a translation is missing.
 */
export interface ErpModule {
  id: string
  /** i18n key of the module title (side menu, bar of the Home screen). */
  titleKey: string
  icon: LucideIcon
  /** File name (without extension) of the module's first screen in app/(tabs). With sections it is the first section's route. */
  route: string
  /**
   * Permissions that make the module visible when it has no sections: ANY of these (an empty list = everybody).
   * With sections it is the union of the sections' permissions (a test keeps them equal); the sections decide.
   */
  permissions: string[]
  /** The module's business functions, shown as the bottom bar while the user is inside it. */
  sections?: ModuleSection[]
  /** Licensed module code (SO, PO, HRM ...): when the token lists licensed modules, this one must be among them. */
  licensedModule?: string
  /** Position in the bar and the menu (low first). */
  order: number
  attention?: Attention[]
}
