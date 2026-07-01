import { createBrowserRouter, Navigate } from 'react-router-dom'
import { useAuthStore } from '@/stores/auth.store'
import { PageLayout } from '@/components/layout/PageLayout'
import { LoginPage } from '@/pages/auth/LoginPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { OrderListPage } from '@/pages/orders/OrderListPage'
import { OrderDetailPage } from '@/pages/orders/OrderDetailPage'
import { ProductListPage } from '@/pages/products/ProductListPage'
import { InventoryListPage } from '@/pages/inventory/InventoryListPage'
import { PaymentsPage } from '@/pages/payments/PaymentsPage'
import { ReportsPage } from '@/pages/reports/ReportsPage'
import { MonitoringPage } from '@/pages/monitoring/MonitoringPage'
import { DepartmentsPage } from '@/pages/masterdata/DepartmentsPage'
import { PositionsPage } from '@/pages/masterdata/PositionsPage'
import { EmployeesPage } from '@/pages/masterdata/EmployeesPage'
import { UnitsOfMeasurePage } from '@/pages/masterdata/UnitsOfMeasurePage'
import { GeographyPage } from '@/pages/masterdata/GeographyPage'
import { LanguagesPage } from '@/pages/masterdata/LanguagesPage'

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
      { path: 'orders/:id', element: <OrderDetailPage /> },
      { path: 'products', element: <ProductListPage /> },
      { path: 'inventory', element: <InventoryListPage /> },
      { path: 'payments', element: <PaymentsPage /> },
      { path: 'reports', element: <ReportsPage /> },
      { path: 'customers', element: <PlaceholderPage title="Customers (CRM)" /> },
      { path: 'settings', element: <PlaceholderPage title="Settings" /> },
      { path: 'monitoring', element: <MonitoringPage /> },
      { path: 'masterdata/departments', element: <DepartmentsPage /> },
      { path: 'masterdata/positions', element: <PositionsPage /> },
      { path: 'masterdata/employees', element: <EmployeesPage /> },
      { path: 'masterdata/units-of-measure', element: <UnitsOfMeasurePage /> },
      { path: 'masterdata/geography', element: <GeographyPage /> },
      { path: 'masterdata/languages', element: <LanguagesPage /> },
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
