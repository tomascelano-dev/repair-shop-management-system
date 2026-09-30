import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { useAuth } from '../auth/AuthContext'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { Button } from '../components/ui/Button'

const schema = z.object({
  email: z.string().min(3),
  password: z.string().min(6)
})

type FormValues = z.infer<typeof schema>

export default function LoginPage() {
  const nav = useNavigate()
  const { login, isAuthenticated } = useAuth()

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: 'admin@local', password: 'admin1234' }
  })

  useEffect(() => {
    if (isAuthenticated) nav('/app', { replace: true })
  }, [isAuthenticated, nav])

  const onSubmit = form.handleSubmit(async (values) => {
    const res = await login(values.email, values.password)
    if (res.ok) {
      toast.success('Bienvenido')
      nav('/app')
    } else {
      toast.error(res.error.title ?? 'No se pudo iniciar sesión', {
        description: res.error.detail
      })
    }
  })

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <Card>
          <CardHeader>
            <CardTitle>RepairShop</CardTitle>
            <p className="mt-1 text-sm text-slate-500">Iniciá sesión para acceder al panel.</p>
          </CardHeader>
          <CardContent>
            <form onSubmit={onSubmit} className="space-y-3">
              <div>
                <label className="text-sm font-medium">Email</label>
                <Input {...form.register('email')} placeholder="admin@local" />
              </div>
              <div>
                <label className="text-sm font-medium">Contraseña</label>
                <Input {...form.register('password')} type="password" placeholder="••••••" />
              </div>

              <div className="pt-2 flex items-center justify-between gap-3">
                <div className="text-xs text-slate-500">
                  DEV seed: <span className="font-mono">admin@local / admin1234</span>
                  <br />
                  <span className="font-mono">tech@local / tech1234</span>
                </div>
                <Button variant="primary" disabled={form.formState.isSubmitting}>
                  {form.formState.isSubmitting ? 'Entrando…' : 'Entrar'}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <div className="mt-4 text-xs text-slate-500">
          En desarrollo, el frontend usa proxy a <span className="font-mono">VITE_PROXY_TARGET</span> (default:
          <span className="font-mono"> http://localhost:8080</span>).
        </div>
      </div>
    </div>
  )
}
