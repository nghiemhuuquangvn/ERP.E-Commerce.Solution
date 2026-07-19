import { createBrowserRouter, Navigate } from 'react-router-dom'
import { useAuthStore } from '@/stores/auth.store'
import { PageLayout } from '@/components/layout/PageLayout'
import { LoginPage } from '@/pages/auth/LoginPage'
import { TwoFactorPage } from '@/pages/auth/TwoFactorPage'
import { TwitterCallbackPage } from '@/pages/auth/TwitterCallbackPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { OrderListPage } from '@/pages/orders/OrderListPage'
import { OrderDetailPage } from '@/pages/orders/OrderDetailPage'
import { OrderFormPage } from '@/pages/orders/OrderFormPage'
import { PurchaseOrderListPage } from '@/pages/orders/PurchaseOrderListPage'
import { PurchaseOrderDetailPage } from '@/pages/orders/PurchaseOrderDetailPage'
import { PurchaseOrderFormPage } from '@/pages/orders/PurchaseOrderFormPage'
import { ProductListPage } from '@/pages/products/ProductListPage'
import { InventoryListPage } from '@/pages/inventory/InventoryListPage'
import { PaymentsPage } from '@/pages/payments/PaymentsPage'
import { ReportsPage } from '@/pages/reports/ReportsPage'
import { MonitoringPage } from '@/pages/monitoring/MonitoringPage'
import { LogsPage } from '@/pages/logs/LogsPage'
import { DepartmentsPage } from '@/pages/masterdata/DepartmentsPage'
import { PositionsPage } from '@/pages/masterdata/PositionsPage'
import { EmployeesPage } from '@/pages/masterdata/EmployeesPage'
import { UnitsOfMeasurePage } from '@/pages/masterdata/UnitsOfMeasurePage'
import { GeographyPage } from '@/pages/masterdata/GeographyPage'
import { LanguagesPage } from '@/pages/masterdata/LanguagesPage'
import { CustomersPage } from '@/pages/crm/CustomersPage'
import { AuthSettingsPage } from '@/pages/settings/AuthSettingsPage'
import { GeneralSettingsPage } from '@/pages/settings/GeneralSettingsPage'
import { CompanySettingsPage } from '@/pages/settings/CompanySettingsPage'
import { NotificationSettingsPage } from '@/pages/settings/NotificationSettingsPage'
import { EmailSettingsPage } from '@/pages/settings/EmailSettingsPage'
import { SmsSettingsPage } from '@/pages/settings/SmsSettingsPage'
import { WorkflowSettingsPage } from '@/pages/settings/WorkflowSettingsPage'
import { FileStorageSettingsPage } from '@/pages/settings/FileStorageSettingsPage'
import { SchedulerSettingsPage } from '@/pages/settings/SchedulerSettingsPage'
import { IntegrationSettingsPage } from '@/pages/settings/IntegrationSettingsPage'
import { ApiSettingsPage } from '@/pages/settings/ApiSettingsPage'
import { AuditSettingsPage } from '@/pages/settings/AuditSettingsPage'
import { AdvancedSettingsPage } from '@/pages/settings/AdvancedSettingsPage'
import { LocalizationSettingsPage } from '@/pages/settings/LocalizationSettingsPage'
import { FeatureFlagsPage } from '@/pages/settings/FeatureFlagsPage'
import { LicensePage } from '@/pages/settings/LicensePage'
import { StoreProductSettingsPage } from '@/pages/settings/StoreProductSettingsPage'
import { CartCheckoutSettingsPage } from '@/pages/settings/CartCheckoutSettingsPage'
import { OrderSettingsPage } from '@/pages/settings/OrderSettingsPage'
import { InventorySettingsPage } from '@/pages/settings/InventorySettingsPage'
import { PaymentSettingsPage } from '@/pages/settings/PaymentSettingsPage'
import { ShippingSettingsPage } from '@/pages/settings/ShippingSettingsPage'
import { TaxSettingsPage } from '@/pages/settings/TaxSettingsPage'
import { CurrencySettingsPage } from '@/pages/settings/CurrencySettingsPage'
import { UsersPage } from '@/pages/identity/UsersPage'
import { RolesPermissionsPage } from '@/pages/identity/RolesPermissionsPage'
import { AttendancePage } from '@/pages/hrm/AttendancePage'
import { LeaveRequestsPage } from '@/pages/hrm/LeaveRequestsPage'
import { PayrollPage } from '@/pages/hrm/PayrollPage'
import { WorkShiftsPage } from '@/pages/hrm/WorkShiftsPage'

function RequireAuth({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return <>{children}</>
}

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/login/2fa',
    element: <TwoFactorPage />,
  },
  {
    path: '/login/oauth/twitter/callback',
    element: <TwitterCallbackPage />,
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <PageLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <Navigate to="/dashboard" replace /> },
      { path: 'dashboard', element: <DashboardPage /> },
      { path: 'orders', element: <OrderListPage /> },
      { path: 'orders/new', element: <OrderFormPage /> },
      { path: 'orders/purchase', element: <PurchaseOrderListPage /> },
      { path: 'orders/purchase/new', element: <PurchaseOrderFormPage /> },
      { path: 'orders/purchase/:id', element: <PurchaseOrderDetailPage /> },
      { path: 'orders/:id', element: <OrderDetailPage /> },
      { path: 'products', element: <ProductListPage /> },
      { path: 'inventory', element: <InventoryListPage /> },
      { path: 'payments', element: <PaymentsPage /> },
      { path: 'reports', element: <ReportsPage /> },
      { path: 'customers', element: <CustomersPage /> },
      { path: 'settings', element: <PlaceholderPage title="Settings" /> },
      { path: 'settings/general', element: <GeneralSettingsPage /> },
      { path: 'settings/company', element: <CompanySettingsPage /> },
      { path: 'settings/auth', element: <AuthSettingsPage /> },
      { path: 'settings/notification', element: <NotificationSettingsPage /> },
      { path: 'settings/email', element: <EmailSettingsPage /> },
      { path: 'settings/sms', element: <SmsSettingsPage /> },
      { path: 'settings/workflow', element: <WorkflowSettingsPage /> },
      { path: 'settings/file-storage', element: <FileStorageSettingsPage /> },
      { path: 'settings/scheduler', element: <SchedulerSettingsPage /> },
      { path: 'settings/integration', element: <IntegrationSettingsPage /> },
      { path: 'settings/api', element: <ApiSettingsPage /> },
      { path: 'settings/audit', element: <AuditSettingsPage /> },
      { path: 'settings/advanced', element: <AdvancedSettingsPage /> },
      { path: 'settings/localization', element: <LocalizationSettingsPage /> },
      { path: 'settings/feature-flags', element: <FeatureFlagsPage /> },
      { path: 'settings/license', element: <LicensePage /> },
      { path: 'settings/store-product', element: <StoreProductSettingsPage /> },
      { path: 'settings/cart-checkout', element: <CartCheckoutSettingsPage /> },
      { path: 'settings/order', element: <OrderSettingsPage /> },
      { path: 'settings/inventory', element: <InventorySettingsPage /> },
      { path: 'settings/payment', element: <PaymentSettingsPage /> },
      { path: 'settings/shipping', element: <ShippingSettingsPage /> },
      { path: 'settings/tax', element: <TaxSettingsPage /> },
      { path: 'settings/currency', element: <CurrencySettingsPage /> },
      { path: 'monitoring', element: <MonitoringPage /> },
      { path: 'logs', element: <LogsPage /> },
      { path: 'masterdata/departments', element: <DepartmentsPage /> },
      { path: 'masterdata/positions', element: <PositionsPage /> },
      { path: 'masterdata/employees', element: <EmployeesPage /> },
      { path: 'masterdata/units-of-measure', element: <UnitsOfMeasurePage /> },
      { path: 'masterdata/geography', element: <GeographyPage /> },
      { path: 'masterdata/languages', element: <LanguagesPage /> },
      { path: 'identity/users', element: <UsersPage /> },
      { path: 'identity/roles', element: <RolesPermissionsPage /> },
      { path: 'hrm/attendance', element: <AttendancePage /> },
      { path: 'hrm/leave-requests', element: <LeaveRequestsPage /> },
      { path: 'hrm/payroll', element: <PayrollPage /> },
      { path: 'hrm/work-shifts', element: <WorkShiftsPage /> },
      // Kept for anyone with the old bookmark/link — SharedMasterDataService now
      // owns Employee data, not HRM.
      { path: 'employees', element: <Navigate to="/masterdata/employees" replace /> },
    ],
  },
])

function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="flex h-40 items-center justify-center rounded-lg border-2 border-dashed border-gray-200">
      <p className="text-gray-400">{title} — coming soon</p>
    </div>
  )
}
