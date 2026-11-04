import { CalendarCheck, CalendarOff, Clock, Users, Wallet } from 'lucide-react-native'
import type { ErpModule } from '@/modules/types'
import { hrmApi } from '@/modules/hrm/api'

export const hrmModule: ErpModule = {
  id: 'hrm',
  titleKey: 'modules.hrm',
  icon: Users,
  route: 'hrm',
  permissions: ['hrm:employees:view', 'hrm:leave:manage', 'hrm:attendance:view', 'hrm:payroll:manage', 'hrm:shifts:view'],
  sections: [
    { id: 'hrm-employees', titleKey: 'sections.employees', icon: Users, route: 'hrm', permissions: ['hrm:employees:view'], order: 10 },
    { id: 'hrm-leave', titleKey: 'sections.leave', icon: CalendarOff, route: 'hrm-leave', permissions: ['hrm:leave:manage'], order: 20 },
    { id: 'hrm-attendance', titleKey: 'sections.attendance', icon: CalendarCheck, route: 'hrm-attendance', permissions: ['hrm:attendance:view'], order: 30 },
    { id: 'hrm-payslips', titleKey: 'sections.payslips', icon: Wallet, route: 'hrm-payslips', permissions: ['hrm:payroll:manage'], order: 40 },
    { id: 'hrm-shifts', titleKey: 'sections.shifts', icon: Clock, route: 'hrm-shifts', permissions: ['hrm:shifts:view'], order: 50 },
  ],
  licensedModule: 'HRM',
  order: 40,
  attention: [
    {
      key: 'leave-waiting',
      labelKey: 'home.leaveWaiting',
      permission: 'hrm:leave:manage',
      count: () => hrmApi.pendingLeaveCount(),
      href: { pathname: '/hrm-leave', params: { status: 'Pending' } },
    },
  ],
}
