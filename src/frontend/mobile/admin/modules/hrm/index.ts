import { Users } from 'lucide-react-native'
import type { ErpModule } from '@/modules/types'
import { hrmApi } from '@/modules/hrm/api'

export const hrmModule: ErpModule = {
  id: 'hrm',
  titleKey: 'modules.hrm',
  icon: Users,
  route: 'hrm',
  permissions: ['hrm:employees:view', 'hrm:leave:manage', 'hrm:attendance:view'],
  licensedModule: 'HRM',
  order: 40,
  attention: [
    {
      key: 'leave-waiting',
      labelKey: 'home.leaveWaiting',
      permission: 'hrm:leave:manage',
      count: () => hrmApi.pendingLeaveCount(),
      href: { pathname: '/hrm', params: { tab: 'leave' } },
    },
  ],
}
