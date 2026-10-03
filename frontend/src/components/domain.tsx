import { useEffect, useRef, useState, type PointerEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { Customer, OrderStatus, Priority } from '../api/types'
import { customersApi } from '../api/endpoints'
import { money } from '../lib/format'
import { ORDER_STATUS, PRIORITY } from '../lib/labels'
import { useDebounced } from '../lib/hooks'
import { cn } from '../lib/cn'
import { Badge, Button, Input, Spinner } from './ui'
import '../lib/scanner'

export function StatusBadge({ status }: { status: OrderStatus }) {
  const s = ORDER_STATUS[status] ?? { label: status, tone: 'slate' as const }
  return <Badge tone={s.tone}>{s.label}</Badge>
}

export function PriorityBadge({ priority, hideNormal }: { priority: Priority; hideNormal?: boolean }) {
  if (hideNormal && priority === 'Normal') return null
  const p = PRIORITY[priority]
  return <Badge tone={p.tone}>{p.label}</Badge>
}

export function Money({ value, currency, className, colorize }: { value: number | null | undefined; currency?: string; className?: string; colorize?: boolean }) {
  return (
    <span className={cn('tabular-nums', colorize && value !== null && value !== undefined && (value > 0 ? 'text-rose-700' : 'text-emerald-700'), className)}>
      {money(value, currency)}
    </span>
  )
}

/** Searches customers as you type (name, phone in any format, email, document). */
export function CustomerSearch({ onSelect, autoFocus, placeholder = 'Buscar cliente por nombre, teléfono o DNI…' }: { onSelect: (c: Customer) => void; autoFocus?: boolean; placeholder?: string }) {
  const [q, setQ] = useState('')
  const debounced = useDebounced(q.trim(), 250)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const query = useQuery({
    queryKey: ['customers', 'search', debounced],
    queryFn: () => customersApi.search({ q: debounced, take: 8 }),
    enabled: debounced.length >= 2,
  })
  const items = query.data?.items ?? []

  useEffect(() => setActive(0), [debounced])

  return (
    <div className="relative">
      <Input
        value={q}
        autoFocus={autoFocus}
        placeholder={placeholder}
        onChange={(e) => {
          setQ(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        onKeyDown={(e) => {
          if (!open || items.length === 0) return
          if (e.key === 'ArrowDown') {
            e.preventDefault()
            setActive((a) => Math.min(a + 1, items.length - 1))
          } else if (e.key === 'ArrowUp') {
            e.preventDefault()
            setActive((a) => Math.max(a - 1, 0))
          } else if (e.key === 'Enter') {
            e.preventDefault()
            onSelect(items[active]!)
            setOpen(false)
          }
        }}
        role="combobox"
        aria-expanded={open}
        aria-autocomplete="list"
        aria-label="Buscar cliente"
      />
      {open && debounced.length >= 2 ? (
        <ul role="listbox" className="absolute z-20 mt-1 max-h-72 w-full overflow-y-auto rounded-lg border border-slate-200 bg-white py-1 text-sm shadow-lg">
          {query.isFetching && items.length === 0 ? (
            <li className="flex items-center gap-2 px-3 py-2 text-slate-500">
              <Spinner className="h-4 w-4" /> Buscando…
            </li>
          ) : items.length === 0 ? (
            <li className="px-3 py-2 text-slate-500">Sin resultados para “{debounced}”.</li>
          ) : (
            items.map((c, i) => (
              <li
                key={c.id}
                role="option"
                aria-selected={i === active}
                onMouseDown={(e) => {
                  e.preventDefault()
                  onSelect(c)
                  setOpen(false)
                }}
                onMouseEnter={() => setActive(i)}
                className={cn('cursor-pointer px-3 py-2', i === active && 'bg-brand-50')}
              >
                <span className="font-medium text-slate-800">{c.fullName}</span>
                <span className="ml-2 text-slate-500">{c.phone}</span>
                {c.documentNumber ? <span className="ml-2 text-xs text-slate-400">Doc. {c.documentNumber}</span> : null}
              </li>
            ))
          )}
        </ul>
      ) : null}
    </div>
  )
}

/** Signature drawn with finger/mouse/pen; returns a PNG data URL. */
export function SignaturePad({ onChange, height = 180 }: { onChange: (dataUrl: string | null) => void; height?: number }) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const drawing = useRef(false)
  const hasInk = useRef(false)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ratio = window.devicePixelRatio || 1
    const width = canvas.offsetWidth
    canvas.width = width * ratio
    canvas.height = height * ratio
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.scale(ratio, ratio)
    ctx.lineWidth = 2.2
    ctx.lineCap = 'round'
    ctx.lineJoin = 'round'
    ctx.strokeStyle = '#0f172a'
  }, [height])

  function point(e: PointerEvent<HTMLCanvasElement>) {
    const rect = e.currentTarget.getBoundingClientRect()
    return { x: e.clientX - rect.left, y: e.clientY - rect.top }
  }

  return (
    <div>
      <canvas
        ref={canvasRef}
        style={{ height, touchAction: 'none' }}
        className="w-full cursor-crosshair rounded-lg border border-dashed border-slate-300 bg-white"
        aria-label="Área de firma"
        onPointerDown={(e) => {
          const ctx = e.currentTarget.getContext('2d')
          if (!ctx) return
          drawing.current = true
          e.currentTarget.setPointerCapture(e.pointerId)
          const p = point(e)
          ctx.beginPath()
          ctx.moveTo(p.x, p.y)
        }}
        onPointerMove={(e) => {
          if (!drawing.current) return
          const ctx = e.currentTarget.getContext('2d')
          if (!ctx) return
          const p = point(e)
          ctx.lineTo(p.x, p.y)
          ctx.stroke()
          hasInk.current = true
        }}
        onPointerUp={(e) => {
          drawing.current = false
          if (hasInk.current) onChange(canvasRef.current?.toDataURL('image/png') ?? null)
          e.currentTarget.releasePointerCapture(e.pointerId)
        }}
      />
      <div className="mt-1 flex items-center justify-between text-xs text-slate-500">
        <span>Firmá con el dedo o el mouse dentro del recuadro.</span>
        <Button
          size="sm"
          variant="ghost"
          onClick={() => {
            const c = canvasRef.current
            const ctx = c?.getContext('2d')
            if (c && ctx) ctx.clearRect(0, 0, c.width, c.height)
            hasInk.current = false
            onChange(null)
          }}
        >
          Borrar
        </Button>
      </div>
    </div>
  )
}

/** Scans EAN/UPC/Code128/QR codes with the device camera (when the browser supports BarcodeDetector). */
export function CameraScanner({ onDetected, onClose }: { onDetected: (code: string) => void; onClose: () => void }) {
  const videoRef = useRef<HTMLVideoElement | null>(null)
  const [error, setError] = useState<string | null>(null)
  // Keep the latest callback without restarting the camera on every parent render.
  const detectedRef = useRef(onDetected)
  useEffect(() => {
    detectedRef.current = onDetected
  }, [onDetected])

  useEffect(() => {
    let stream: MediaStream | null = null
    let stopped = false
    let timer: number | undefined

    async function start() {
      try {
        if (!window.BarcodeDetector) throw new Error('Este navegador no permite escanear con la cámara. Usá un lector USB o escribí el código.')
        const detector = new window.BarcodeDetector({ formats: ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'code_39', 'qr_code'] })
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false })
        if (!videoRef.current) return
        videoRef.current.srcObject = stream
        await videoRef.current.play()
        const tick = async () => {
          if (stopped || !videoRef.current) return
          try {
            const codes = await detector.detect(videoRef.current)
            if (codes.length > 0 && codes[0]!.rawValue) {
              detectedRef.current(codes[0]!.rawValue)
              return
            }
          } catch {
            // frame not ready yet
          }
          timer = window.setTimeout(tick, 250)
        }
        void tick()
      } catch (e) {
        setError(e instanceof Error ? e.message : 'No se pudo acceder a la cámara.')
      }
    }

    void start()
    return () => {
      stopped = true
      if (timer) window.clearTimeout(timer)
      stream?.getTracks().forEach((t) => t.stop())
    }
  }, [])

  return (
    <div className="space-y-3">
      {error ? (
        <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">{error}</p>
      ) : (
        <video ref={videoRef} className="aspect-video w-full rounded-lg bg-black object-cover" muted playsInline />
      )}
      <div className="flex justify-end">
        <Button onClick={onClose}>Cerrar</Button>
      </div>
    </div>
  )
}
