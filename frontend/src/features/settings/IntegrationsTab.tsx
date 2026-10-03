import { useId, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { settingsApi } from '../../api/endpoints'
import { errorMessage } from '../../api/http'
import type { FiscalEnvironment, IntegrationsStatus } from '../../api/types'
import { Alert, Badge, Button, Card, Checkbox, ErrorState, Field, Input, Loading, Select } from '../../components/ui'
import { readFileAsBase64 } from '../../lib/files'

export function IntegrationsTab() {
  const status = useQuery({ queryKey: ['integrations'], queryFn: settingsApi.integrations })
  if (status.isLoading) return <Loading />
  if (status.isError || !status.data) return <ErrorState error={errorMessage(status.error)} onRetry={() => void status.refetch()} />
  const s = status.data
  return (
    <div className="space-y-4">
      <MercadoPagoCard status={s} />
      <FiscalCard status={s} />
      <Card title="Servicios del servidor">
        <p className="mb-3 text-sm text-slate-600">Se configuran en el servidor (variables de entorno). Acá ves si están activos.</p>
        <ul className="grid gap-2 text-sm sm:grid-cols-2">
          <li className="flex items-center justify-between rounded-lg border border-slate-200 px-3 py-2">
            <span>Almacenamiento de fotos</span>
            <Badge tone="blue">{s.storageProvider === 'S3' ? 'S3 / R2' : 'Disco local'}</Badge>
          </li>
          {Object.entries(s.notificationChannels).map(([channel, on]) => (
            <li key={channel} className="flex items-center justify-between rounded-lg border border-slate-200 px-3 py-2">
              <span>Envío por {channel === 'Sms' ? 'SMS' : channel}</span>
              <Badge tone={on ? 'green' : 'slate'}>{on ? 'Activo' : 'No configurado'}</Badge>
            </li>
          ))}
          <li className="flex items-center justify-between rounded-lg border border-slate-200 px-3 py-2">
            <span>Sugerencias con IA (Claude)</span>
            <Badge tone={s.aiConfigured ? 'green' : 'slate'}>{s.aiConfigured ? 'Activo' : 'No configurado'}</Badge>
          </li>
        </ul>
      </Card>
    </div>
  )
}

function SecretState({ configured }: { configured: boolean }) {
  return configured ? <Badge tone="green">guardado</Badge> : <Badge tone="slate">sin cargar</Badge>
}

function MercadoPagoCard({ status }: { status: IntegrationsStatus }) {
  const queryClient = useQueryClient()
  const [enabled, setEnabled] = useState(status.mercadoPagoEnabled)
  const [token, setToken] = useState('')
  const [secret, setSecret] = useState('')
  const webhookId = useId()
  const save = useMutation({
    mutationFn: () => settingsApi.updateMercadoPago({ enabled, accessToken: token.trim() || null, webhookSecret: secret.trim() || null }),
    onSuccess: (s) => {
      queryClient.setQueryData(['integrations'], s)
      setToken('')
      setSecret('')
      toast.success('Mercado Pago actualizado')
    },
    onError: (err) => toast.error('No se pudo guardar', { description: errorMessage(err) }),
  })
  return (
    <Card title="Mercado Pago · links de pago" actions={<Badge tone={status.mercadoPagoEnabled ? 'green' : 'slate'}>{status.mercadoPagoEnabled ? 'Activo' : 'Inactivo'}</Badge>}>
      <div className="space-y-3">
        <p className="text-sm text-slate-600">Genera links de pago para el saldo de una orden. Cuando el cliente paga, el pago se registra solo en la orden.</p>
        <Checkbox checked={enabled} onChange={(e) => setEnabled(e.target.checked)} label="Habilitar cobros con Mercado Pago" />
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={<span className="flex items-center gap-2">Access token <SecretState configured={status.mercadoPagoAccessTokenConfigured} /></span>} hint="Credenciales de producción de tu cuenta (empieza con APP_USR-). Se guarda cifrado.">
            <Input type="password" autoComplete="off" value={token} onChange={(e) => setToken(e.target.value)} placeholder={status.mercadoPagoAccessTokenConfigured ? '•••••••• (dejar vacío para no cambiar)' : 'APP_USR-…'} />
          </Field>
          <Field label={<span className="flex items-center gap-2">Clave secreta del webhook <SecretState configured={status.mercadoPagoWebhookSecretConfigured} /></span>} hint="Tus integraciones → Webhooks → Clave secreta.">
            <Input type="password" autoComplete="off" value={secret} onChange={(e) => setSecret(e.target.value)} placeholder={status.mercadoPagoWebhookSecretConfigured ? '•••••••• (dejar vacío para no cambiar)' : ''} />
          </Field>
        </div>
        <Field label="URL para notificaciones (webhook)" hint="Pegala en Mercado Pago → Tus integraciones → Webhooks, evento “Pagos”." controlId={webhookId}>
          <div className="flex gap-2">
            <Input id={webhookId} readOnly value={status.mercadoPagoWebhookUrl} onFocus={(e) => e.target.select()} />
            <Button
              onClick={() => {
                void navigator.clipboard?.writeText(status.mercadoPagoWebhookUrl)
                toast.success('URL copiada')
              }}
            >
              Copiar
            </Button>
          </div>
        </Field>
        {enabled && !status.mercadoPagoAccessTokenConfigured && !token ? <Alert tone="amber">Cargá el access token para poder generar links.</Alert> : null}
        <div className="flex justify-end">
          <Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </div>
      </div>
    </Card>
  )
}

function FiscalCard({ status }: { status: IntegrationsStatus }) {
  const queryClient = useQueryClient()
  const [enabled, setEnabled] = useState(status.fiscalEnabled)
  const [environment, setEnvironment] = useState<FiscalEnvironment>(status.fiscalEnvironment)
  const [pointOfSale, setPointOfSale] = useState(String(status.fiscalPointOfSale || 1))
  const [file, setFile] = useState<File | null>(null)
  const [password, setPassword] = useState('')
  const save = useMutation({
    mutationFn: async () =>
      settingsApi.updateFiscal({
        enabled,
        environment,
        pointOfSale: Number(pointOfSale) || 1,
        certificatePfxBase64: file ? await readFileAsBase64(file) : null,
        certificatePassword: file ? password : null,
      }),
    onSuccess: (s) => {
      queryClient.setQueryData(['integrations'], s)
      setFile(null)
      setPassword('')
      toast.success('Facturación electrónica actualizada')
    },
    onError: (err) => toast.error('No se pudo guardar', { description: errorMessage(err) }),
  })
  return (
    <Card title="ARCA (ex AFIP) · factura electrónica" actions={<Badge tone={status.fiscalEnabled ? 'green' : 'slate'}>{status.fiscalEnabled ? (status.fiscalEnvironment === 'Produccion' ? 'Producción' : 'Homologación') : 'Inactivo'}</Badge>}>
      <div className="space-y-3">
        <p className="text-sm text-slate-600">
          Emite facturas A, B o C con CAE según tu condición frente al IVA y la del cliente. Necesitás un certificado digital (.pfx/.p12) asociado al servicio “wsfe” y un punto de venta “Web Services”.
        </p>
        <Checkbox checked={enabled} onChange={(e) => setEnabled(e.target.checked)} label="Habilitar facturación electrónica" />
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="Ambiente">
            <Select value={environment} onChange={(e) => setEnvironment(e.target.value as FiscalEnvironment)}>
              <option value="Homologacion">Homologación (pruebas)</option>
              <option value="Produccion">Producción</option>
            </Select>
          </Field>
          <Field label="Punto de venta">
            <Input type="number" min={1} value={pointOfSale} onChange={(e) => setPointOfSale(e.target.value)} />
          </Field>
          <Field label={<span className="flex items-center gap-2">Certificado <SecretState configured={status.fiscalCertificateConfigured} /></span>}>
            <Input type="file" accept=".pfx,.p12,application/x-pkcs12" className="py-1.5" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          </Field>
          <Field label="Contraseña del certificado">
            <Input type="password" autoComplete="off" value={password} disabled={!file} onChange={(e) => setPassword(e.target.value)} />
          </Field>
        </div>
        {environment === 'Produccion' && enabled ? <Alert tone="amber">En producción los comprobantes tienen validez fiscal. Probá primero en homologación.</Alert> : null}
        <div className="flex justify-end">
          <Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>
            Guardar
          </Button>
        </div>
      </div>
    </Card>
  )
}
