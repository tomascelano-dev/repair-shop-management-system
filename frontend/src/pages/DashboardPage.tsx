import { useQuery } from '@tanstack/react-query'
import { dashboardApi } from '../api/repairshop'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card'
import { Badge } from '../components/ui/Badge'

export default function DashboardPage() {
  const q = useQuery({
    queryKey: ['dashboard', 'summary'],
    queryFn: () => dashboardApi.summary()
  })

  if (q.isLoading) return <div className="text-sm text-slate-600">Cargando…</div>
  if (q.isError) return <div className="text-sm text-rose-700">No se pudo cargar el dashboard.</div>

  const d = q.data

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Resumen</CardTitle>
          <div className="text-xs text-slate-500">Generado: {new Date(d.generatedAtUtc).toLocaleString()}</div>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
            <Stat label="Total" value={d.totalOrders} />
            <Stat label="Abiertas" value={d.openOrders} />
            <Stat label="Listas" value={d.readyOrders} />
            <Stat label="Entregadas" value={d.deliveredOrders} />
            <Stat label="Canceladas" value={d.cancelledOrders} />
          </div>

          <div className="mt-4">
            <Badge>
              Cobros: {d.paymentsCurrency ? `${d.totalPaymentsAmount} ${d.paymentsCurrency}` : 'moneda mixta / N/A'}
            </Badge>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-slate-50 p-3">
      <div className="text-xs text-slate-500">{label}</div>
      <div className="text-xl font-semibold">{value}</div>
    </div>
  )
}
