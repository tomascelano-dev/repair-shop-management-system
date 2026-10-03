import { useState, type FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { authApi } from '../../api/endpoints'
import { errorMessage, fieldErrors } from '../../api/http'
import { useSession } from '../../auth/session'
import { Button, Card, ConfirmDialog, Field, Input, KeyValue, PageHeader } from '../../components/ui'
import { ROLE } from '../../lib/labels'

export function ProfilePage() {
  const { user, shops, acceptSession, logout } = useSession()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [askLogoutAll, setAskLogoutAll] = useState(false)

  const change = useMutation({
    mutationFn: () => authApi.changePassword(current, next),
    onSuccess: (session) => {
      acceptSession(session)
      setCurrent('')
      setNext('')
      setConfirm('')
      setErrors({})
      toast.success('Contraseña actualizada', { description: 'Se cerraron tus sesiones en otros dispositivos.' })
    },
    onError: (err) => {
      setErrors(fieldErrors(err))
      toast.error('No se pudo cambiar la contraseña', { description: errorMessage(err) })
    },
  })

  const logoutAll = useMutation({
    mutationFn: authApi.logoutAll,
    onSuccess: async () => {
      toast.success('Cerraste sesión en todos los dispositivos')
      await logout()
    },
    onError: (err) => toast.error('No se pudo cerrar las sesiones', { description: errorMessage(err) }),
  })

  function submit(e: FormEvent) {
    e.preventDefault()
    if (next !== confirm) {
      setErrors({ confirm: 'Las contraseñas no coinciden.' })
      return
    }
    if (next.length < 8 || !/[a-zA-Z]/.test(next) || !/\d/.test(next)) {
      setErrors({ newPassword: 'Usá al menos 8 caracteres, con letras y números.' })
      return
    }
    change.mutate()
  }

  if (!user) return null

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title="Mi cuenta" />
      <div className="space-y-4">
        <Card title="Datos">
          <KeyValue
            items={[
              { label: 'Nombre', value: user.displayName },
              { label: 'Email', value: user.email },
              { label: 'Rol', value: ROLE[user.role] },
              { label: 'Sucursal actual', value: user.shopName ?? '—' },
              { label: 'Sucursales con acceso', value: shops.map((s) => `${s.shopName} (${ROLE[s.role]})`).join(', '), hidden: shops.length < 2 },
            ]}
          />
        </Card>

        <Card title="Cambiar contraseña">
          <form onSubmit={submit} className="space-y-3">
            <Field label="Contraseña actual" error={errors.currentPassword}>
              <Input type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} />
            </Field>
            <Field label="Nueva contraseña" error={errors.newPassword} hint="Mínimo 8 caracteres, con letras y números. Una frase fácil de recordar es más segura que una palabra rara.">
              <Input type="password" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} />
            </Field>
            <Field label="Repetí la nueva contraseña" error={errors.confirm}>
              <Input type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
            </Field>
            <div className="flex justify-end">
              <Button type="submit" variant="primary" loading={change.isPending} disabled={!current || !next || !confirm}>
                Cambiar contraseña
              </Button>
            </div>
          </form>
        </Card>

        <Card title="Seguridad">
          <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
            <p className="text-slate-600">¿Usaste el sistema en una computadora compartida o perdiste el celular? Cerrá todas tus sesiones abiertas.</p>
            <Button variant="danger" onClick={() => setAskLogoutAll(true)}>
              Cerrar sesión en todos lados
            </Button>
          </div>
        </Card>
      </div>
      <ConfirmDialog
        open={askLogoutAll}
        title="¿Cerrar todas las sesiones?"
        message="Vas a tener que volver a ingresar en todos tus dispositivos, incluido este."
        danger
        confirmLabel="Cerrar todas"
        loading={logoutAll.isPending}
        onConfirm={() => logoutAll.mutate()}
        onClose={() => setAskLogoutAll(false)}
      />
    </div>
  )
}
