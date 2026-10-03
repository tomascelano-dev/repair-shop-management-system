import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import type { Permission } from '../../api/types'
import { errorMessage } from '../../api/http'
import { useSession } from '../../auth/session'
import { cn } from '../../lib/cn'
import { initials } from '../../lib/format'
import { ROLE } from '../../lib/labels'

interface NavItem {
  to: string
  label: string
  icon: string
  permission?: Permission
  end?: boolean
}

const NAV: { section: string; items: NavItem[] }[] = [
  {
    section: 'Taller',
    items: [
      { to: '/', label: 'Inicio', icon: '◧', end: true },
      { to: '/orders', label: 'Órdenes', icon: '☰', end: true },
      { to: '/orders/board', label: 'Tablero', icon: '▦' },
      { to: '/orders/new', label: 'Nueva orden', icon: '＋', permission: 'orders.manage' },
      { to: '/customers', label: 'Clientes', icon: '☺' },
    ],
  },
  {
    section: 'Ventas',
    items: [
      { to: '/pos', label: 'Punto de venta', icon: '⌁', permission: 'sales' },
      { to: '/sales', label: 'Ventas', icon: '≡', permission: 'sales' },
      { to: '/cash', label: 'Caja', icon: '$', permission: 'sales' },
      { to: '/invoices', label: 'Facturas', icon: '▤', permission: 'sales' },
    ],
  },
  {
    section: 'Stock',
    items: [
      { to: '/inventory', label: 'Inventario', icon: '▣' },
      { to: '/purchases', label: 'Compras', icon: '⇩', permission: 'inventory.manage' },
      { to: '/transfers', label: 'Transferencias', icon: '⇄', permission: 'inventory.manage' },
    ],
  },
  {
    section: 'Gestión',
    items: [
      { to: '/reports', label: 'Reportes', icon: '◔', permission: 'reports' },
      { to: '/messages', label: 'Mensajes', icon: '✉', permission: 'orders.work' },
      { to: '/settings', label: 'Configuración', icon: '⚙', permission: 'admin' },
    ],
  },
]

export function AppLayout() {
  const { user, shops, can, logout, switchShop } = useSession()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [switching, setSwitching] = useState(false)
  const navigate = useNavigate()

  const nav = NAV.map((s) => ({ ...s, items: s.items.filter((i) => !i.permission || can(i.permission)) })).filter((s) => s.items.length > 0)

  async function onSwitch(shopId: string) {
    if (shopId === user?.shopId) return
    setSwitching(true)
    try {
      await switchShop(shopId)
      navigate('/')
      toast.success('Cambiaste de sucursal')
    } catch (err) {
      toast.error('No se pudo cambiar de sucursal', { description: errorMessage(err) })
    } finally {
      setSwitching(false)
    }
  }

  const sidebar = (
    <nav aria-label="Principal" className="flex h-full flex-col">
      <div className="flex items-center gap-2 px-4 py-4">
        <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-600 text-sm font-bold text-white">RS</span>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-white">RepairShop</p>
          <p className="truncate text-xs text-slate-400">{user?.shopName}</p>
        </div>
      </div>
      <div className="flex-1 space-y-5 overflow-y-auto px-2 pb-4">
        {nav.map((section) => (
          <div key={section.section}>
            <p className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wider text-slate-500">{section.section}</p>
            <ul className="space-y-0.5">
              {section.items.map((item) => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.end}
                    onClick={() => setMobileOpen(false)}
                    className={({ isActive }) =>
                      cn('flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm transition', isActive ? 'bg-white/10 font-medium text-white' : 'text-slate-300 hover:bg-white/5 hover:text-white')
                    }
                  >
                    <span aria-hidden="true" className="w-4 text-center text-slate-400">
                      {item.icon}
                    </span>
                    {item.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </nav>
  )

  return (
    <div className="flex min-h-full">
      <aside className="no-print fixed inset-y-0 left-0 hidden w-60 bg-slate-900 lg:block">{sidebar}</aside>

      {mobileOpen ? (
        <div className="no-print fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-slate-900/50" onClick={() => setMobileOpen(false)} />
          <aside className="absolute inset-y-0 left-0 w-64 bg-slate-900 shadow-xl">{sidebar}</aside>
        </div>
      ) : null}

      <div className="flex min-w-0 flex-1 flex-col lg:pl-60">
        <header className="no-print sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-slate-200 bg-white/90 px-4 backdrop-blur">
          <button type="button" className="rounded-md p-2 text-slate-600 hover:bg-slate-100 lg:hidden" onClick={() => setMobileOpen(true)} aria-label="Abrir menú">
            ☰
          </button>
          <div className="flex-1" />
          {shops.length > 1 ? (
            <label className="flex items-center gap-2 text-sm">
              <span className="hidden text-slate-500 sm:inline">Sucursal</span>
              <select
                className="h-9 rounded-lg border border-slate-300 bg-white px-2 text-sm"
                value={user?.shopId}
                disabled={switching}
                onChange={(e) => void onSwitch(e.target.value)}
                aria-label="Cambiar de sucursal"
              >
                {shops.map((s) => (
                  <option key={s.shopId} value={s.shopId}>
                    {s.shopName}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <span className="hidden text-sm text-slate-500 sm:inline">{user?.shopName}</span>
          )}
          <UserMenu name={user?.displayName ?? ''} role={user ? ROLE[user.role] : ''} onLogout={logout} />
        </header>
        <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

function UserMenu({ name, role, onLogout }: { name: string; role: string; onLogout: () => Promise<void> }) {
  const [open, setOpen] = useState(false)
  const navigate = useNavigate()
  return (
    <div className="relative">
      <button type="button" onClick={() => setOpen((o) => !o)} className="flex items-center gap-2 rounded-lg px-2 py-1 hover:bg-slate-100" aria-haspopup="menu" aria-expanded={open}>
        <span className="flex h-8 w-8 items-center justify-center rounded-full bg-brand-100 text-xs font-semibold text-brand-800">{initials(name)}</span>
        <span className="hidden text-left text-sm leading-tight sm:block">
          <span className="block font-medium text-slate-800">{name}</span>
          <span className="block text-xs text-slate-500">{role}</span>
        </span>
      </button>
      {open ? (
        <>
          <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} />
          <div role="menu" className="absolute right-0 z-20 mt-1 w-48 overflow-hidden rounded-lg border border-slate-200 bg-white py-1 text-sm shadow-lg">
            <button role="menuitem" className="block w-full px-3 py-2 text-left hover:bg-slate-50" onClick={() => { setOpen(false); navigate('/profile') }}>
              Mi cuenta
            </button>
            <button role="menuitem" className="block w-full px-3 py-2 text-left text-rose-700 hover:bg-rose-50" onClick={() => { setOpen(false); void onLogout() }}>
              Cerrar sesión
            </button>
          </div>
        </>
      ) : null}
    </div>
  )
}
