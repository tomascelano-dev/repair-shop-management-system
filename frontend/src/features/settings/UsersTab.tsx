import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { settingsApi, usersApi } from '../../api/endpoints'
import { errorMessage, refreshSession } from '../../api/http'
import type { Branch, Role, UserAdmin, UserLink } from '../../api/types'
import { useSession } from '../../auth/session'
import { Alert, Badge, Button, Card, Checkbox, ConfirmDialog, EmptyState, ErrorState, Field, Input, Loading, Modal, Select, Table, Td, Th } from '../../components/ui'
import { dateTime, relative } from '../../lib/format'
import { ROLE } from '../../lib/labels'

const ROLES: Role[] = ['Admin', 'Tech', 'Reception', 'Cashier']

const ROLE_HELP: Record<Role, string> = {
  Admin: 'Todo, incluida la configuración, usuarios y anulaciones.',
  Tech: 'Trabaja las órdenes: diagnóstico, presupuesto, repuestos, notas y estados.',
  Reception: 'Clientes, ingreso y entrega de equipos, presupuestos y cobros.',
  Cashier: 'Punto de venta, caja, cobros y entrega de equipos.',
}

// ===== Users =====

export function UsersTab() {
  const { user: me } = useSession()
  const users = useQuery({ queryKey: ['users'], queryFn: usersApi.list })
  const [inviting, setInviting] = useState(false)
  const [editing, setEditing] = useState<UserAdmin | null>(null)
  const [link, setLink] = useState<{ title: string; link: UserLink } | null>(null)

  if (users.isLoading) return <Loading />
  if (users.isError) return <ErrorState error={errorMessage(users.error)} onRetry={() => void users.refetch()} />

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-slate-600">Cada persona entra con su usuario: así queda registrado quién hizo cada cosa.</p>
        <Button variant="primary" onClick={() => setInviting(true)}>
          + Invitar usuario
        </Button>
      </div>
      <Card padded={false}>
        <Table>
          <thead className="bg-slate-50">
            <tr>
              <Th>Usuario</Th>
              <Th>Rol</Th>
              <Th>Sucursales</Th>
              <Th>Último ingreso</Th>
              <Th>Estado</Th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {users.data!.map((u) => (
              <tr key={u.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setEditing(u)}>
                <Td>
                  <span className="font-medium text-slate-800">{u.displayName}</span>
                  {u.id === me?.id ? <span className="ml-1 text-xs text-slate-400">(vos)</span> : null}
                  <span className="block text-xs text-slate-500">{u.email}</span>
                </Td>
                <Td>{ROLE[u.role]}</Td>
                <Td className="text-xs text-slate-600">{u.shops.map((s) => s.shopName).join(', ')}</Td>
                <Td className="text-xs text-slate-500">{u.lastLoginAtUtc ? relative(u.lastLoginAtUtc) : 'Nunca'}</Td>
                <Td>
                  {u.hasPendingInvitation ? <Badge tone="amber">Invitación pendiente</Badge> : u.isActive ? <Badge tone="green">Activo</Badge> : <Badge tone="slate">Inactivo</Badge>}
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      </Card>

      {inviting ? (
        <InviteDialog
          onClose={() => setInviting(false)}
          onDone={(l) => {
            setInviting(false)
            setLink({ title: `Invitación para ${l.user.displayName}`, link: l })
          }}
        />
      ) : null}
      {editing ? (
        <UserDialog
          user={editing}
          isMe={editing.id === me?.id}
          onClose={() => setEditing(null)}
          onLink={(l) => {
            setEditing(null)
            setLink({ title: `Restablecer contraseña de ${l.user.displayName}`, link: l })
          }}
        />
      ) : null}
      {link ? <LinkDialog title={link.title} link={link.link} onClose={() => setLink(null)} /> : null}
    </div>
  )
}

function InviteDialog({ onClose, onDone }: { onClose: () => void; onDone: (link: UserLink) => void }) {
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  const [name, setName] = useState('')
  const [role, setRole] = useState<Role>('Tech')
  const invite = useMutation({
    mutationFn: () => usersApi.invite({ email: email.trim(), displayName: name.trim(), role }),
    onSuccess: (l) => {
      void queryClient.invalidateQueries({ queryKey: ['users'] })
      onDone(l)
    },
    onError: (err) => toast.error('No se pudo invitar', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title="Invitar usuario"
      description="Se genera un link para que la persona elija su contraseña."
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={invite.isPending} disabled={!email.includes('@') || name.trim().length < 2} onClick={() => invite.mutate()}>
            Crear invitación
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Nombre" required>
          <Input value={name} onChange={(e) => setName(e.target.value)} autoFocus />
        </Field>
        <Field label="Email" required>
          <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </Field>
        <Field label="Rol" hint={ROLE_HELP[role]}>
          <Select value={role} onChange={(e) => setRole(e.target.value as Role)}>
            {ROLES.map((r) => (
              <option key={r} value={r}>
                {ROLE[r]}
              </option>
            ))}
          </Select>
        </Field>
      </div>
    </Modal>
  )
}

function LinkDialog({ title, link, onClose }: { title: string; link: UserLink; onClose: () => void }) {
  const text = `Hola ${link.user.displayName}, entrá a este link para crear tu contraseña: ${link.url}`
  return (
    <Modal
      open
      onClose={onClose}
      title={title}
      description={`Vence ${dateTime(link.expiresAtUtc)}. Compartilo solo con esa persona.`}
      footer={
        <Button variant="primary" onClick={onClose}>
          Listo
        </Button>
      }
    >
      <div className="space-y-3">
        <div className="flex gap-2">
          <Input readOnly value={link.url} onFocus={(e) => e.target.select()} aria-label="Link" />
          <Button
            onClick={() => {
              void navigator.clipboard?.writeText(link.url)
              toast.success('Link copiado')
            }}
          >
            Copiar
          </Button>
        </div>
        <div className="flex flex-wrap gap-2">
          <a className="text-sm text-brand-700 underline" href={`https://wa.me/?text=${encodeURIComponent(text)}`} target="_blank" rel="noreferrer">
            Enviar por WhatsApp
          </a>
          <a className="text-sm text-brand-700 underline" href={`mailto:${link.user.email}?subject=${encodeURIComponent('Acceso al sistema del taller')}&body=${encodeURIComponent(text)}`}>
            Enviar por email
          </a>
        </div>
      </div>
    </Modal>
  )
}

function UserDialog({ user, isMe, onClose, onLink }: { user: UserAdmin; isMe: boolean; onClose: () => void; onLink: (l: UserLink) => void }) {
  const queryClient = useQueryClient()
  const branches = useQuery({ queryKey: ['branches'], queryFn: settingsApi.branches })
  const [name, setName] = useState(user.displayName)
  const [role, setRole] = useState<Role>(user.role)
  const [active, setActive] = useState(user.isActive)
  const [shopRole, setShopRole] = useState<Record<string, Role>>({})
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['users'] })

  const save = useMutation({
    mutationFn: () => usersApi.update(user.id, { displayName: name.trim(), role, isActive: active }),
    onSuccess: () => {
      toast.success('Usuario actualizado')
      refresh()
      onClose()
    },
    onError: (err) => toast.error('No se pudo guardar', { description: errorMessage(err) }),
  })
  const reset = useMutation({
    mutationFn: () => usersApi.resetLink(user.id),
    onSuccess: (l) => onLink(l),
    onError: (err) => toast.error('No se pudo generar el link', { description: errorMessage(err) }),
  })
  const grant = useMutation({
    mutationFn: ({ shopId, r }: { shopId: string; r: Role }) => usersApi.grantShop(user.id, shopId, r),
    onSuccess: () => {
      toast.success('Acceso actualizado')
      refresh()
    },
    onError: (err) => toast.error('No se pudo dar acceso', { description: errorMessage(err) }),
  })
  const revoke = useMutation({
    mutationFn: (shopId: string) => usersApi.revokeShop(user.id, shopId),
    onSuccess: () => {
      toast.success('Acceso quitado')
      refresh()
    },
    onError: (err) => toast.error('No se pudo quitar el acceso', { description: errorMessage(err) }),
  })

  const current = queryClient.getQueryData<UserAdmin[]>(['users'])?.find((u) => u.id === user.id) ?? user
  const access = new Map(current.shops.map((s) => [s.shopId, s]))

  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={user.displayName}
      description={`${user.email} · creado ${dateTime(user.createdAtUtc)}`}
      footer={
        <>
          <Button variant="ghost" className="mr-auto" loading={reset.isPending} onClick={() => reset.mutate()}>
            {user.hasPendingInvitation ? 'Nuevo link de invitación' : 'Link para cambiar contraseña'}
          </Button>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={save.isPending} disabled={name.trim().length < 2} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Nombre">
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </Field>
          <Field label="Rol en esta sucursal" hint={ROLE_HELP[role]}>
            <Select value={role} disabled={isMe} onChange={(e) => setRole(e.target.value as Role)}>
              {ROLES.map((r) => (
                <option key={r} value={r}>
                  {ROLE[r]}
                </option>
              ))}
            </Select>
          </Field>
          <Checkbox checked={active} disabled={isMe} onChange={(e) => setActive(e.target.checked)} label="Usuario activo" description="Desactivarlo cierra sus sesiones al instante." />
        </div>
        {isMe ? <Alert tone="slate">No podés cambiar tu propio rol ni desactivarte.</Alert> : null}

        <div>
          <p className="mb-2 text-sm font-medium text-slate-700">Acceso a sucursales</p>
          {branches.isLoading ? (
            <Loading />
          ) : (
            <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 text-sm">
              {(branches.data ?? []).map((b) => {
                const a = access.get(b.id)
                const r = shopRole[b.id] ?? a?.role ?? 'Tech'
                return (
                  <li key={b.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                    <span>
                      {b.name} {a?.isHome ? <Badge tone="blue">principal</Badge> : null}
                      {!b.isActive ? <Badge tone="slate">inactiva</Badge> : null}
                    </span>
                    <span className="flex items-center gap-2">
                      {b.isCurrent || a?.isHome ? (
                        <span className="text-xs text-slate-500">
                          {a ? ROLE[a.role] : ''} · {b.isCurrent ? 'se edita arriba' : 'sucursal principal'}
                        </span>
                      ) : (
                        <Select className="h-8 w-36" value={r} onChange={(e) => setShopRole((x) => ({ ...x, [b.id]: e.target.value as Role }))} aria-label={`Rol en ${b.name}`}>
                          {ROLES.map((x) => (
                            <option key={x} value={x}>
                              {ROLE[x]}
                            </option>
                          ))}
                        </Select>
                      )}
                      {b.isCurrent || a?.isHome ? null : a ? (
                        <>
                          {r !== a.role ? (
                            <Button size="sm" loading={grant.isPending} onClick={() => grant.mutate({ shopId: b.id, r })}>
                              Guardar
                            </Button>
                          ) : null}
                          <Button size="sm" variant="ghost" loading={revoke.isPending} onClick={() => revoke.mutate(b.id)}>
                            Quitar
                          </Button>
                        </>
                      ) : (
                        <Button size="sm" loading={grant.isPending} onClick={() => grant.mutate({ shopId: b.id, r })}>
                          Dar acceso
                        </Button>
                      )}
                    </span>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      </div>
    </Modal>
  )
}

// ===== Branches =====

export function BranchesTab() {
  const queryClient = useQueryClient()
  const branches = useQuery({ queryKey: ['branches'], queryFn: settingsApi.branches })
  const [creating, setCreating] = useState(false)
  const [toggling, setToggling] = useState<Branch | null>(null)
  const toggle = useMutation({
    mutationFn: (b: Branch) => settingsApi.setBranchActive(b.id, !b.isActive),
    onSuccess: () => {
      toast.success('Sucursal actualizada')
      setToggling(null)
      void queryClient.invalidateQueries({ queryKey: ['branches'] })
    },
    onError: (err) => toast.error('No se pudo actualizar', { description: errorMessage(err) }),
  })

  if (branches.isLoading) return <Loading />
  if (branches.isError) return <ErrorState error={errorMessage(branches.error)} onRetry={() => void branches.refetch()} />

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-slate-600">Cada sucursal tiene sus órdenes, stock, caja y numeración. Los clientes y reportes consolidados se ven desde el inicio.</p>
        <Button variant="primary" onClick={() => setCreating(true)}>
          + Nueva sucursal
        </Button>
      </div>
      {(branches.data ?? []).length === 0 ? (
        <EmptyState title="Sin sucursales" />
      ) : (
        <Card padded={false}>
          <Table>
            <thead className="bg-slate-50">
              <tr>
                <Th>Sucursal</Th>
                <Th>Dirección</Th>
                <Th>Tu rol</Th>
                <Th>Estado</Th>
                <Th />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {branches.data!.map((b) => (
                <tr key={b.id}>
                  <Td className="font-medium">
                    {b.name} {b.isCurrent ? <Badge tone="blue">actual</Badge> : null}
                  </Td>
                  <Td className="text-slate-600">{[b.addressLine, b.city].filter(Boolean).join(', ') || '—'}</Td>
                  <Td>{b.myRole ? ROLE[b.myRole] : <span className="text-xs text-slate-400">sin acceso</span>}</Td>
                  <Td>{b.isActive ? <Badge tone="green">Activa</Badge> : <Badge tone="slate">Inactiva</Badge>}</Td>
                  <Td align="right">
                    {!b.isCurrent ? (
                      <Button size="sm" variant="ghost" onClick={() => setToggling(b)}>
                        {b.isActive ? 'Desactivar' : 'Activar'}
                      </Button>
                    ) : null}
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        </Card>
      )}
      {creating ? <NewBranchDialog onClose={() => setCreating(false)} /> : null}
      <ConfirmDialog
        open={!!toggling}
        title={toggling?.isActive ? `¿Desactivar ${toggling.name}?` : `¿Activar ${toggling?.name ?? ''}?`}
        message={toggling?.isActive ? 'Nadie podrá trabajar en esa sucursal hasta reactivarla. Los datos se conservan.' : undefined}
        danger={toggling?.isActive}
        confirmLabel={toggling?.isActive ? 'Desactivar' : 'Activar'}
        loading={toggle.isPending}
        onConfirm={() => toggling && toggle.mutate(toggling)}
        onClose={() => setToggling(null)}
      />
    </div>
  )
}

function NewBranchDialog({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient()
  const [v, setV] = useState({ name: '', phone: '', addressLine: '', city: '', copyTemplates: true, copyCatalog: true })
  const create = useMutation({
    mutationFn: () => settingsApi.createBranch({ name: v.name.trim(), phone: v.phone.trim() || null, addressLine: v.addressLine.trim() || null, city: v.city.trim() || null, copyTemplates: v.copyTemplates, copyCatalog: v.copyCatalog }),
    onSuccess: async (b) => {
      toast.success(`Sucursal ${b.name} creada`, { description: 'Ya podés cambiar a ella desde el selector de arriba.' })
      void queryClient.invalidateQueries({ queryKey: ['branches'] })
      // New branch access is part of the session (shop switcher): refresh it.
      await refreshSession()
      onClose()
    },
    onError: (err) => toast.error('No se pudo crear la sucursal', { description: errorMessage(err) }),
  })
  return (
    <Modal
      open
      onClose={onClose}
      title="Nueva sucursal"
      description="Hereda la configuración de esta sucursal (moneda, términos, avisos). Vos quedás como administrador."
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant="primary" loading={create.isPending} disabled={v.name.trim().length < 2} onClick={() => create.mutate()}>
            Crear sucursal
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Nombre" required className="sm:col-span-2">
          <Input value={v.name} onChange={(e) => setV({ ...v, name: e.target.value })} autoFocus placeholder="Sucursal Centro" />
        </Field>
        <Field label="Teléfono">
          <Input value={v.phone} onChange={(e) => setV({ ...v, phone: e.target.value })} />
        </Field>
        <Field label="Ciudad">
          <Input value={v.city} onChange={(e) => setV({ ...v, city: e.target.value })} />
        </Field>
        <Field label="Dirección" className="sm:col-span-2">
          <Input value={v.addressLine} onChange={(e) => setV({ ...v, addressLine: e.target.value })} />
        </Field>
        <Checkbox className="sm:col-span-2" checked={v.copyTemplates} onChange={(e) => setV({ ...v, copyTemplates: e.target.checked })} label="Copiar las plantillas de mensajes" />
        <Checkbox className="sm:col-span-2" checked={v.copyCatalog} onChange={(e) => setV({ ...v, copyCatalog: e.target.checked })} label="Copiar el catálogo de inventario (sin stock)" description="Para después transferir stock entre sucursales." />
      </div>
    </Modal>
  )
}
