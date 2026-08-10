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
import { QuotationListPage } from '@/pages/sales/QuotationListPage'
import { QuotationFormPage } from '@/pages/sales/QuotationFormPage'
import { QuotationDetailPage } from '@/pages/sales/QuotationDetailPage'
import { CustomerCreditLimitsPage } from '@/pages/sales/CustomerCreditLimitsPage'
import { CommissionPlansPage } from '@/pages/sales/CommissionPlansPage'
import { CommissionRecordsPage } from '@/pages/sales/CommissionRecordsPage'
import { ProductListPage } from '@/pages/products/ProductListPage'
import { ProductFormPage } from '@/pages/products/ProductFormPage'
import { BrandsPage } from '@/pages/products/BrandsPage'
import { CategoriesPage } from '@/pages/products/CategoriesPage'
import { PriceListsPage } from '@/pages/products/PriceListsPage'
import { ProductAttributesPage } from '@/pages/products/ProductAttributesPage'
import { SupplierListPage } from '@/pages/purchasing/SupplierListPage'
import { SupplierFormPage } from '@/pages/purchasing/SupplierFormPage'
import { PurchaseRequestsPage } from '@/pages/purchasing/PurchaseRequestsPage'
import { InventoryListPage } from '@/pages/inventory/InventoryListPage'
import { InventoryDetailPage } from '@/pages/inventory/InventoryDetailPage'
import { LocationsPage } from '@/pages/inventory/LocationsPage'
import { WarehousesPage } from '@/pages/inventory/WarehousesPage'
import { StockAdjustmentsPage } from '@/pages/inventory/StockAdjustmentsPage'
import { StocktakeListPage } from '@/pages/inventory/StocktakeListPage'
import { StocktakeDetailPage } from '@/pages/inventory/StocktakeDetailPage'
import { TransferOrderListPage } from '@/pages/inventory/TransferOrderListPage'
import { TransferOrderFormPage } from '@/pages/inventory/TransferOrderFormPage'
import { BarcodeResolvePage } from '@/pages/inventory/BarcodeResolvePage'
import { InventoryValuationSnapshotsPage } from '@/pages/inventory/InventoryValuationSnapshotsPage'
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
import { CompaniesPage } from '@/pages/masterdata/CompaniesPage'
import { BranchesPage } from '@/pages/masterdata/BranchesPage'
import { DivisionsPage } from '@/pages/masterdata/DivisionsPage'
import { TeamsPage } from '@/pages/masterdata/TeamsPage'
import { BusinessUnitsPage } from '@/pages/masterdata/BusinessUnitsPage'
import { CostCentersPage } from '@/pages/masterdata/CostCentersPage'
import { ProfitCentersPage } from '@/pages/masterdata/ProfitCentersPage'
import { OrganizationCalendarsPage } from '@/pages/masterdata/OrganizationCalendarsPage'
import { EmployeeTeamAssignmentsPage } from '@/pages/masterdata/EmployeeTeamAssignmentsPage'
import { CurrenciesPage } from '@/pages/masterdata/CurrenciesPage'
import { PaymentTermsPage } from '@/pages/masterdata/PaymentTermsPage'
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
import { AccountsPage } from '@/pages/accounting/AccountsPage'
import { FiscalPeriodsPage } from '@/pages/accounting/FiscalPeriodsPage'
import { TaxRatesPage } from '@/pages/accounting/TaxRatesPage'
import { InvoicesPage } from '@/pages/accounting/InvoicesPage'
import { BillsPage } from '@/pages/accounting/BillsPage'
import { ReportsPage as AccountingReportsPage } from '@/pages/accounting/ReportsPage'
import { JournalEntriesPage } from '@/pages/accounting/JournalEntriesPage'
import { BankAccountsPage } from '@/pages/accounting/BankAccountsPage'
import { AssistantPanel } from '@/pages/chatbot/AssistantPanel'
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
      { path: 'sales/quotations', element: <QuotationListPage /> },
      { path: 'sales/quotations/new', element: <QuotationFormPage /> },
      { path: 'sales/quotations/:id', element: <QuotationDetailPage /> },
      { path: 'sales/credit-limits', element: <CustomerCreditLimitsPage /> },
      { path: 'sales/commission-plans', element: <CommissionPlansPage /> },
      { path: 'sales/commissions', element: <CommissionRecordsPage /> },
      { path: 'products', element: <ProductListPage /> },
      { path: 'products/new', element: <ProductFormPage /> },
      { path: 'products/brands', element: <BrandsPage /> },
      { path: 'products/categories', element: <CategoriesPage /> },
      { path: 'products/pricing', element: <PriceListsPage /> },
      { path: 'products/attributes', element: <ProductAttributesPage /> },
      { path: 'products/:id', element: <ProductFormPage /> },
      { path: 'purchasing/suppliers', element: <SupplierListPage /> },
      { path: 'purchasing/suppliers/new', element: <SupplierFormPage /> },
      { path: 'purchasing/suppliers/:id', element: <SupplierFormPage /> },
      { path: 'purchasing/requests', element: <PurchaseRequestsPage /> },
      { path: 'inventory', element: <InventoryListPage /> },
      { path: 'inventory/locations', element: <LocationsPage /> },
      { path: 'inventory/warehouses', element: <WarehousesPage /> },
      { path: 'inventory/adjustments', element: <StockAdjustmentsPage /> },
      { path: 'inventory/stocktakes', element: <StocktakeListPage /> },
      { path: 'inventory/stocktakes/:id', element: <StocktakeDetailPage /> },
      { path: 'inventory/transfers', element: <TransferOrderListPage /> },
      { path: 'inventory/transfers/new', element: <TransferOrderFormPage /> },
      { path: 'inventory/barcode', element: <BarcodeResolvePage /> },
      { path: 'inventory/valuation-snapshots', element: <InventoryValuationSnapshotsPage /> },
      { path: 'inventory/:productId', element: <InventoryDetailPage /> },
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
      { path: 'chatbot', element: <AssistantPanel /> },
      { path: 'monitoring', element: <MonitoringPage /> },
      { path: 'logs', element: <LogsPage /> },
      { path: 'masterdata/departments', element: <DepartmentsPage /> },
      { path: 'masterdata/positions', element: <PositionsPage /> },
      { path: 'masterdata/employees', element: <EmployeesPage /> },
      { path: 'masterdata/units-of-measure', element: <UnitsOfMeasurePage /> },
      { path: 'masterdata/geography', element: <GeographyPage /> },
      { path: 'masterdata/languages', element: <LanguagesPage /> },
      { path: 'masterdata/companies', element: <CompaniesPage /> },
      { path: 'masterdata/branches', element: <BranchesPage /> },
      { path: 'masterdata/divisions', element: <DivisionsPage /> },
      { path: 'masterdata/teams', element: <TeamsPage /> },
      { path: 'masterdata/business-units', element: <BusinessUnitsPage /> },
      { path: 'masterdata/cost-centers', element: <CostCentersPage /> },
      { path: 'masterdata/profit-centers', element: <ProfitCentersPage /> },
      { path: 'masterdata/organization-calendars', element: <OrganizationCalendarsPage /> },
      { path: 'masterdata/employee-team-assignments', element: <EmployeeTeamAssignmentsPage /> },
      { path: 'masterdata/currencies', element: <CurrenciesPage /> },
      { path: 'masterdata/payment-terms', element: <PaymentTermsPage /> },
      { path: 'identity/users', element: <UsersPage /> },
      { path: 'identity/roles', element: <RolesPermissionsPage /> },
      { path: 'accounting/accounts', element: <AccountsPage /> },
      { path: 'accounting/fiscal-periods', element: <FiscalPeriodsPage /> },
      { path: 'accounting/tax-rates', element: <TaxRatesPage /> },
      { path: 'accounting/invoices', element: <InvoicesPage /> },
      { path: 'accounting/bills', element: <BillsPage /> },
      { path: 'accounting/reports', element: <AccountingReportsPage /> },
      { path: 'accounting/journal-entries', element: <JournalEntriesPage /> },
      { path: 'accounting/bank-accounts', element: <BankAccountsPage /> },
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
