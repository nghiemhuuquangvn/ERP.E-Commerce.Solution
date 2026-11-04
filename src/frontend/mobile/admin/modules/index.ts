import type { ErpModule } from '@/modules/types'
import { dashboardModule } from '@/modules/dashboard'
import { purchaseModule } from '@/modules/purchase'
import { salesModule } from '@/modules/sales'
import { hrmModule } from '@/modules/hrm'

/**
 * Every module of the app, in one place. To add a module (docs/frontend/mobile-admin-app.md):
 *  1. create modules/<id>/ with its api, screens and an exported ErpModule,
 *  2. add it to this list,
 *  3. add the tab screen app/(tabs)/<route>.tsx (and detail routes) and the i18n keys.
 * test/modules.test.ts fails when a listed module has no route file or no title translation.
 */
export const MODULES: ErpModule[] = [dashboardModule, purchaseModule, salesModule, hrmModule]
