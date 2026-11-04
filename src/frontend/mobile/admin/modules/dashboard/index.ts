import { Home } from 'lucide-react-native'
import type { ErpModule } from '@/modules/types'

/** Home: the sales summary and the "needs your action" list (built from the other modules' `attention`). Open to every signed-in user. */
export const dashboardModule: ErpModule = {
  id: 'dashboard',
  titleKey: 'modules.dashboard',
  icon: Home,
  route: 'index',
  permissions: [],
  order: 10,
}
