import { lazy, Suspense, type ComponentType, type ReactNode } from 'react'
import { Link, Route, Routes } from 'react-router-dom'
import type { Permission } from './api/types'
import { RequireAuth, RequirePermission } from './auth/guards'
import { AppLayout } from './components/layout/AppLayout'
import { Loading } from './components/ui'
import { AcceptInvitationPage, ForgotPasswordPage, LoginPage, ResetPasswordPage } from './features/auth/AuthPages'

// Route-level code splitting: each section downloads only when it is opened.
function page<K extends string>(loader: () => Promise<Record<K, ComponentType>>, name: K) {
  return lazy(async () => ({ default: (await loader())[name] }))
}

const DashboardPage = page(() => import('./features/dashboard/DashboardPage'), 'DashboardPage')
const OrdersPage = page(() => import('./features/orders/OrdersPage'), 'OrdersPage')
const BoardPage = page(() => import('./features/orders/BoardPage'), 'BoardPage')
const NewOrderPage = page(() => import('./features/orders/NewOrderPage'), 'NewOrderPage')
const OrderDetailPage = page(() => import('./features/orders/OrderDetailPage'), 'OrderDetailPage')
const CustomersPage = page(() => import('./features/customers/CustomersPage'), 'CustomersPage')
const CustomerDetailPage = page(() => import('./features/customers/CustomersPage'), 'CustomerDetailPage')
const PosPage = page(() => import('./features/pos/PosPage'), 'PosPage')
const SalesPage = page(() => import('./features/sales/SalesPage'), 'SalesPage')
const InvoicesPage = page(() => import('./features/sales/InvoicesPage'), 'InvoicesPage')
const CashPage = page(() => import('./features/cash/CashPage'), 'CashPage')
const InventoryPage = page(() => import('./features/inventory/InventoryPage'), 'InventoryPage')
const PurchasesPage = page(() => import('./features/inventory/PurchasesPage'), 'PurchasesPage')
const TransfersPage = page(() => import('./features/inventory/TransfersPage'), 'TransfersPage')
const ReportsPage = page(() => import('./features/reports/ReportsPage'), 'ReportsPage')
const MessagesPage = page(() => import('./features/messages/MessagesPage'), 'MessagesPage')
const SettingsPage = page(() => import('./features/settings/SettingsPage'), 'SettingsPage')
const ProfilePage = page(() => import('./features/auth/ProfilePage'), 'ProfilePage')
const TrackingPage = page(() => import('./features/portal/TrackingPage'), 'TrackingPage')
const BillingPage = page(() => import('./features/billing/BillingPage'), 'BillingPage')
const SignupPage = page(() => import('./features/public/SignupPage'), 'SignupPage')
const VerifyEmailPage = page(() => import('./features/public/SignupPage'), 'VerifyEmailPage')
const PricingPage = page(() => import('./features/public/PricingPage'), 'PricingPage')
const TermsPage = page(() => import('./features/public/LegalPages'), 'TermsPage')
const PrivacyPage = page(() => import('./features/public/LegalPages'), 'PrivacyPage')
const RefundPage = page(() => import('./features/public/LegalPages'), 'RefundPage')

function Guard({ permission, children }: { permission: Permission; children: ReactNode }) {
  return <RequirePermission permission={permission}>{children}</RequirePermission>
}

export default function App() {
  return (
    <Suspense fallback={<Loading />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/olvide" element={<ForgotPasswordPage />} />
        <Route path="/restablecer" element={<ResetPasswordPage />} />
        <Route path="/invitacion" element={<AcceptInvitationPage />} />
        <Route path="/t/:token" element={<TrackingPage />} />
        <Route path="/registro" element={<SignupPage />} />
        <Route path="/verificar-email" element={<VerifyEmailPage />} />
        <Route path="/precios" element={<PricingPage />} />
        <Route path="/terminos" element={<TermsPage />} />
        <Route path="/privacidad" element={<PrivacyPage />} />
        <Route path="/reembolsos" element={<RefundPage />} />

        <Route
          element={
            <RequireAuth>
              <AppLayout />
            </RequireAuth>
          }
        >
          <Route index element={<DashboardPage />} />
          <Route path="orders" element={<OrdersPage />} />
          <Route path="orders/board" element={<BoardPage />} />
          <Route path="orders/new" element={<Guard permission="orders.manage"><NewOrderPage /></Guard>} />
          <Route path="orders/:id" element={<OrderDetailPage />} />
          <Route path="customers" element={<CustomersPage />} />
          <Route path="customers/:id" element={<CustomerDetailPage />} />
          <Route path="pos" element={<Guard permission="sales"><PosPage /></Guard>} />
          <Route path="sales" element={<Guard permission="sales"><SalesPage /></Guard>} />
          <Route path="cash" element={<Guard permission="sales"><CashPage /></Guard>} />
          <Route path="invoices" element={<Guard permission="sales"><InvoicesPage /></Guard>} />
          <Route path="inventory" element={<InventoryPage />} />
          <Route path="purchases" element={<Guard permission="inventory.manage"><PurchasesPage /></Guard>} />
          <Route path="transfers" element={<Guard permission="inventory.manage"><TransfersPage /></Guard>} />
          <Route path="reports" element={<Guard permission="reports"><ReportsPage /></Guard>} />
          <Route path="messages" element={<Guard permission="orders.work"><MessagesPage /></Guard>} />
          <Route path="settings" element={<Guard permission="admin"><SettingsPage /></Guard>} />
          <Route path="billing" element={<Guard permission="admin"><BillingPage /></Guard>} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="*" element={<NotFound />} />
        </Route>
      </Routes>
    </Suspense>
  )
}

function NotFound() {
  return (
    <div className="mx-auto max-w-md py-16 text-center">
      <p className="text-lg font-semibold text-slate-900">No encontramos esta página</p>
      <p className="mt-1 text-sm text-slate-500">Puede que el link esté mal o que la sección ya no exista.</p>
      <Link to="/" className="mt-4 inline-block text-sm font-medium text-brand-700 underline">
        Volver al inicio
      </Link>
    </div>
  )
}
