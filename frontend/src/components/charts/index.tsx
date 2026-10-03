import { useState } from 'react'
import { cn } from '../../lib/cn'

/**
 * Small dependency-free SVG charts. Each chart exposes an accessible name and a visually hidden data table,
 * uses one hue per meaning and keeps a zero baseline.
 */

export interface Point {
  label: string
  value: number
  hint?: string
}

function niceMax(max: number) {
  if (max <= 0) return 1
  const exp = Math.pow(10, Math.floor(Math.log10(max)))
  const f = max / exp
  const nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10
  return nice * exp
}

function SrTable({ title, data, format }: { title: string; data: Point[]; format: (v: number) => string }) {
  return (
    <table className="sr-only">
      <caption>{title}</caption>
      <tbody>
        {data.map((d) => (
          <tr key={d.label}>
            <th scope="row">{d.label}</th>
            <td>{format(d.value)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/** Vertical bars for a time series (e.g. daily revenue). */
export function BarChart({ data, title, format = (v) => String(v), height = 220, color = '#2547ea', labelEvery }: { data: Point[]; title: string; format?: (v: number) => string; height?: number; color?: string; labelEvery?: number }) {
  const [hover, setHover] = useState<number | null>(null)
  const width = 720
  const pad = { top: 16, right: 12, bottom: 28, left: 64 }
  const innerW = width - pad.left - pad.right
  const innerH = height - pad.top - pad.bottom
  const max = niceMax(Math.max(0, ...data.map((d) => d.value)))
  const step = data.length > 0 ? innerW / data.length : innerW
  const barW = Math.max(2, Math.min(36, step * 0.7))
  const every = labelEvery ?? Math.max(1, Math.ceil(data.length / 10))
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((t) => t * max)

  if (data.length === 0) return <p className="py-8 text-center text-sm text-slate-500">Sin datos para el período.</p>

  return (
    <figure className="relative">
      <svg viewBox={`0 0 ${width} ${height}`} className="h-auto w-full" role="img" aria-label={title}>
        {ticks.map((t) => {
          const y = pad.top + innerH - (t / max) * innerH
          return (
            <g key={t}>
              <line x1={pad.left} x2={width - pad.right} y1={y} y2={y} stroke="#e2e8f0" />
              <text x={pad.left - 8} y={y} textAnchor="end" dominantBaseline="middle" className="fill-slate-400 text-[10px]">
                {format(t)}
              </text>
            </g>
          )
        })}
        {data.map((d, i) => {
          const h = (Math.max(0, d.value) / max) * innerH
          const x = pad.left + i * step + (step - barW) / 2
          const y = pad.top + innerH - h
          return (
            <g key={d.label} onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover(null)}>
              <rect x={pad.left + i * step} y={pad.top} width={step} height={innerH} fill="transparent" />
              <rect x={x} y={y} width={barW} height={Math.max(h, d.value > 0 ? 1 : 0)} rx={2} fill={color} opacity={hover === null || hover === i ? 1 : 0.45}>
                <title>{`${d.label}: ${format(d.value)}${d.hint ? ` — ${d.hint}` : ''}`}</title>
              </rect>
              {i % every === 0 ? (
                <text x={pad.left + i * step + step / 2} y={height - 8} textAnchor="middle" className="fill-slate-500 text-[10px]">
                  {d.label}
                </text>
              ) : null}
            </g>
          )
        })}
        <line x1={pad.left} x2={width - pad.right} y1={pad.top + innerH} y2={pad.top + innerH} stroke="#94a3b8" />
      </svg>
      {hover !== null && data[hover] ? (
        <div className="pointer-events-none absolute right-2 top-0 rounded-md bg-slate-900 px-2 py-1 text-xs text-white shadow">
          {data[hover]!.label}: <strong>{format(data[hover]!.value)}</strong>
          {data[hover]!.hint ? <span className="block text-slate-300">{data[hover]!.hint}</span> : null}
        </div>
      ) : null}
      <SrTable title={title} data={data} format={format} />
    </figure>
  )
}

/** Horizontal ranking bars (top models, categories, technicians). */
export function RankBars({ data, title, format = (v) => String(v), color = 'bg-brand-500', max: maxItems = 10 }: { data: Point[]; title: string; format?: (v: number) => string; color?: string; max?: number }) {
  const rows = data.slice(0, maxItems)
  const max = Math.max(1, ...rows.map((r) => r.value))
  if (rows.length === 0) return <p className="py-6 text-center text-sm text-slate-500">Sin datos para el período.</p>
  return (
    <figure aria-label={title}>
      <ul className="space-y-2">
        {rows.map((r) => (
          <li key={r.label} className="text-sm">
            <div className="flex justify-between gap-2">
              <span className="truncate text-slate-700" title={r.label}>
                {r.label}
              </span>
              <span className="shrink-0 tabular-nums text-slate-600">
                {format(r.value)}
                {r.hint ? <span className="ml-1 text-xs text-slate-400">{r.hint}</span> : null}
              </span>
            </div>
            <div className="mt-1 h-2 rounded-full bg-slate-100">
              <div className={cn('h-2 rounded-full', color)} style={{ width: `${Math.max(2, (r.value / max) * 100)}%` }} />
            </div>
          </li>
        ))}
      </ul>
      <SrTable title={title} data={rows} format={format} />
    </figure>
  )
}

/** Proportion of a whole as a single stacked bar with legend (e.g. quotes approved/rejected/expired). */
export function StackedBar({ parts, title }: { parts: { label: string; value: number; color: string }[]; title: string }) {
  const total = parts.reduce((s, p) => s + p.value, 0)
  if (total === 0) return <p className="py-6 text-center text-sm text-slate-500">Sin datos para el período.</p>
  return (
    <figure aria-label={title}>
      <div className="flex h-4 overflow-hidden rounded-full bg-slate-100">
        {parts
          .filter((p) => p.value > 0)
          .map((p) => (
            <div key={p.label} className={p.color} style={{ width: `${(p.value / total) * 100}%` }} title={`${p.label}: ${p.value}`} />
          ))}
      </div>
      <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-sm">
        {parts.map((p) => (
          <li key={p.label} className="flex items-center gap-1.5">
            <span className={cn('inline-block h-2.5 w-2.5 rounded-sm', p.color)} aria-hidden="true" />
            <span className="text-slate-600">{p.label}</span>
            <span className="font-medium tabular-nums text-slate-800">{p.value}</span>
          </li>
        ))}
      </ul>
    </figure>
  )
}
