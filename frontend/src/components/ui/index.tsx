import * as React from 'react'
import { useEffect, useId, useRef } from 'react'
import { cn } from '../../lib/cn'
import { TONE_CLASSES, type Tone } from '../../lib/labels'

// ===== Button =====
type Variant = 'default' | 'primary' | 'danger' | 'ghost' | 'success' | 'outline'
type Size = 'sm' | 'md' | 'lg'

export type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant
  size?: Size
  loading?: boolean
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { className, variant = 'default', size = 'md', loading, disabled, children, type = 'button', ...props },
  ref
) {
  return (
    <button
      ref={ref}
      type={type}
      disabled={disabled || loading}
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-lg border font-medium shadow-sm transition focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-1 focus-visible:ring-brand-500 disabled:cursor-not-allowed disabled:opacity-50',
        size === 'sm' && 'h-8 px-2.5 text-xs',
        size === 'md' && 'h-10 px-3.5 text-sm',
        size === 'lg' && 'h-12 px-5 text-base',
        variant === 'default' && 'border-slate-200 bg-white text-slate-800 hover:bg-slate-50',
        variant === 'outline' && 'border-brand-600 bg-white text-brand-700 hover:bg-brand-50',
        variant === 'primary' && 'border-brand-600 bg-brand-600 text-white hover:bg-brand-700',
        variant === 'success' && 'border-emerald-600 bg-emerald-600 text-white hover:bg-emerald-700',
        variant === 'danger' && 'border-rose-600 bg-rose-600 text-white hover:bg-rose-700',
        variant === 'ghost' && 'border-transparent bg-transparent text-slate-700 shadow-none hover:bg-slate-100',
        className
      )}
      {...props}
    >
      {loading ? <Spinner className="h-4 w-4" /> : null}
      {children}
    </button>
  )
})

// ===== Inputs =====
const fieldClass =
  'w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 shadow-sm outline-none transition placeholder:text-slate-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-200 disabled:bg-slate-100 disabled:text-slate-500 aria-[invalid=true]:border-rose-400 aria-[invalid=true]:ring-rose-100'

export const Input = React.forwardRef<HTMLInputElement, React.InputHTMLAttributes<HTMLInputElement>>(function Input({ className, ...props }, ref) {
  return <input ref={ref} className={cn(fieldClass, 'h-10', className)} {...props} />
})

export const Textarea = React.forwardRef<HTMLTextAreaElement, React.TextareaHTMLAttributes<HTMLTextAreaElement>>(function Textarea({ className, rows = 3, ...props }, ref) {
  return <textarea ref={ref} rows={rows} className={cn(fieldClass, 'py-2', className)} {...props} />
})

export const Select = React.forwardRef<HTMLSelectElement, React.SelectHTMLAttributes<HTMLSelectElement>>(function Select({ className, children, ...props }, ref) {
  return (
    <select ref={ref} className={cn(fieldClass, 'h-10 pr-8', className)} {...props}>
      {children}
    </select>
  )
})

export function Checkbox({ label, description, className, ...props }: React.InputHTMLAttributes<HTMLInputElement> & { label: React.ReactNode; description?: React.ReactNode }) {
  const id = useId()
  return (
    <label htmlFor={props.id ?? id} className={cn('flex cursor-pointer items-start gap-2 text-sm', className)}>
      <input id={props.id ?? id} type="checkbox" className="mt-0.5 h-4 w-4 rounded border-slate-300 text-brand-600 focus:ring-brand-500" {...props} />
      <span>
        <span className="text-slate-800">{label}</span>
        {description ? <span className="block text-xs text-slate-500">{description}</span> : null}
      </span>
    </label>
  )
}

/**
 * Label + control + error/hint. Pass the control as child; the id is wired automatically.
 * For composite children (an input next to a select or button), pass `controlId` and put that id on the input.
 */
export function Field({ label, error, hint, required, className, children, controlId }: { label: React.ReactNode; error?: string; hint?: React.ReactNode; required?: boolean; className?: string; children: React.ReactElement; controlId?: string }) {
  const id = useId()
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined
  const targetId = controlId ?? (children.props as { id?: string }).id ?? id
  const child = controlId
    ? children
    : React.cloneElement(children, {
        id: targetId,
        'aria-invalid': error ? true : undefined,
        'aria-describedby': describedBy,
      } as Record<string, unknown>)
  return (
    <div className={cn('space-y-1', className)}>
      <label htmlFor={targetId} className="block text-sm font-medium text-slate-700">
        {label}
        {required ? <span className="ml-0.5 text-rose-600">*</span> : null}
      </label>
      {child}
      {error ? (
        <p id={`${id}-error`} className="text-xs text-rose-600">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-hint`} className="text-xs text-slate-500">
          {hint}
        </p>
      ) : null}
    </div>
  )
}

// ===== Layout bits =====
export function Card({ className, children, title, actions, padded = true }: { className?: string; children?: React.ReactNode; title?: React.ReactNode; actions?: React.ReactNode; padded?: boolean }) {
  return (
    <section className={cn('rounded-xl border border-slate-200 bg-white shadow-sm', className)}>
      {title || actions ? (
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-4 py-3">
          {title ? <h2 className="text-sm font-semibold text-slate-800">{title}</h2> : <span />}
          {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
        </header>
      ) : null}
      <div className={cn(padded && 'p-4')}>{children}</div>
    </section>
  )
}

export function PageHeader({ title, subtitle, actions }: { title: React.ReactNode; subtitle?: React.ReactNode; actions?: React.ReactNode }) {
  return (
    <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        <h1 className="truncate text-xl font-semibold text-slate-900">{title}</h1>
        {subtitle ? <p className="mt-0.5 text-sm text-slate-500">{subtitle}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
    </div>
  )
}

export function Badge({ tone = 'slate', className, children }: { tone?: Tone; className?: string; children: React.ReactNode }) {
  return <span className={cn('inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', TONE_CLASSES[tone], className)}>{children}</span>
}

export function Spinner({ className }: { className?: string }) {
  return (
    <svg className={cn('h-5 w-5 animate-spin text-current', className)} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
    </svg>
  )
}

export function Loading({ label = 'Cargando…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500" role="status">
      <Spinner />
      {label}
    </div>
  )
}

export function EmptyState({ title, description, action }: { title: string; description?: React.ReactNode; action?: React.ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-slate-300 px-6 py-10 text-center">
      <p className="text-sm font-medium text-slate-700">{title}</p>
      {description ? <p className="mt-1 max-w-md text-sm text-slate-500">{description}</p> : null}
      {action ? <div className="mt-4">{action}</div> : null}
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: string; onRetry?: () => void }) {
  return (
    <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
      <p>{error}</p>
      {onRetry ? (
        <Button size="sm" className="mt-3" onClick={onRetry}>
          Reintentar
        </Button>
      ) : null}
    </div>
  )
}

export function Alert({ tone = 'amber', title, children, className }: { tone?: Tone; title?: React.ReactNode; children?: React.ReactNode; className?: string }) {
  return (
    <div role="status" className={cn('rounded-lg px-3 py-2 text-sm ring-1 ring-inset', TONE_CLASSES[tone], className)}>
      {title ? <p className="font-medium">{title}</p> : null}
      {children ? <div className={cn(title && 'mt-0.5')}>{children}</div> : null}
    </div>
  )
}

export function Stat({ label, value, hint, tone, onClick }: { label: string; value: React.ReactNode; hint?: React.ReactNode; tone?: 'default' | 'warn' | 'bad' | 'good'; onClick?: () => void }) {
  const Comp = onClick ? 'button' : 'div'
  return (
    <Comp
      onClick={onClick}
      className={cn(
        'w-full rounded-xl border bg-white p-4 text-left shadow-sm',
        onClick && 'transition hover:border-brand-300 hover:shadow',
        tone === 'warn' && 'border-amber-200',
        tone === 'bad' && 'border-rose-200',
        tone === 'good' && 'border-emerald-200',
        (!tone || tone === 'default') && 'border-slate-200'
      )}
    >
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</p>
      <p
        className={cn(
          'mt-1 text-2xl font-semibold tabular-nums',
          tone === 'warn' && 'text-amber-700',
          tone === 'bad' && 'text-rose-700',
          tone === 'good' && 'text-emerald-700',
          (!tone || tone === 'default') && 'text-slate-900'
        )}
      >
        {value}
      </p>
      {hint ? <p className="mt-1 text-xs text-slate-500">{hint}</p> : null}
    </Comp>
  )
}

// ===== Table =====
export function Table({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn('overflow-x-auto', className)}>
      <table className="min-w-full divide-y divide-slate-200 text-sm">{children}</table>
    </div>
  )
}

export function Th({ children, className, align = 'left' }: { children?: React.ReactNode; className?: string; align?: 'left' | 'right' | 'center' }) {
  return <th scope="col" className={cn('whitespace-nowrap px-3 py-2 text-xs font-semibold uppercase tracking-wide text-slate-500', align === 'right' && 'text-right', align === 'center' && 'text-center', align === 'left' && 'text-left', className)}>{children}</th>
}

export function Td({ children, className, align = 'left', colSpan }: { children?: React.ReactNode; className?: string; align?: 'left' | 'right' | 'center'; colSpan?: number }) {
  return (
    <td colSpan={colSpan} className={cn('px-3 py-2 align-middle text-slate-700', align === 'right' && 'text-right tabular-nums', align === 'center' && 'text-center', className)}>
      {children}
    </td>
  )
}

export function Pagination({ skip, take, total, onChange }: { skip: number; take: number; total: number; onChange: (skip: number) => void }) {
  if (total <= take) return total > 0 ? <p className="px-1 pt-3 text-xs text-slate-500">{total} resultado{total === 1 ? '' : 's'}</p> : null
  const page = Math.floor(skip / take) + 1
  const pages = Math.max(1, Math.ceil(total / take))
  return (
    <nav className="flex items-center justify-between gap-2 pt-3 text-sm" aria-label="Paginación">
      <p className="text-xs text-slate-500">
        {skip + 1}–{Math.min(skip + take, total)} de {total}
      </p>
      <div className="flex items-center gap-1">
        <Button size="sm" variant="ghost" disabled={page <= 1} onClick={() => onChange(Math.max(0, skip - take))}>
          ← Anterior
        </Button>
        <span className="px-2 text-xs text-slate-600">
          Página {page} de {pages}
        </span>
        <Button size="sm" variant="ghost" disabled={page >= pages} onClick={() => onChange(skip + take)}>
          Siguiente →
        </Button>
      </div>
    </nav>
  )
}

// ===== Tabs =====
export function Tabs<T extends string>({ value, onChange, tabs, className }: { value: T; onChange: (v: T) => void; tabs: { value: T; label: React.ReactNode; count?: number; hidden?: boolean }[]; className?: string }) {
  return (
    <div role="tablist" className={cn('flex gap-1 overflow-x-auto border-b border-slate-200', className)}>
      {tabs
        .filter((t) => !t.hidden)
        .map((t) => (
          <button
            key={t.value}
            role="tab"
            type="button"
            aria-selected={t.value === value}
            onClick={() => onChange(t.value)}
            className={cn(
              '-mb-px whitespace-nowrap border-b-2 px-3 py-2 text-sm font-medium transition',
              t.value === value ? 'border-brand-600 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-800'
            )}
          >
            {t.label}
            {t.count !== undefined ? <span className="ml-1.5 rounded-full bg-slate-100 px-1.5 text-xs text-slate-600">{t.count}</span> : null}
          </button>
        ))}
    </div>
  )
}

// ===== Modal (native <dialog>: focus trap + Escape for free) =====
export function Modal({ open, onClose, title, description, children, footer, size = 'md' }: { open: boolean; onClose: () => void; title: React.ReactNode; description?: React.ReactNode; children: React.ReactNode; footer?: React.ReactNode; size?: 'sm' | 'md' | 'lg' | 'xl' }) {
  const ref = useRef<HTMLDialogElement | null>(null)
  const titleId = useId()
  const descriptionId = useId()

  useEffect(() => {
    const dlg = ref.current
    if (!dlg) return
    if (open && !dlg.open) {
      if (typeof dlg.showModal === 'function') dlg.showModal()
      else dlg.setAttribute('open', '')
    }
    if (!open && dlg.open) dlg.close()
  }, [open])

  if (!open) return null

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onCancel={(e) => {
        e.preventDefault()
        onClose()
      }}
      onClick={(e) => {
        if (e.target === ref.current) onClose()
      }}
      className={cn(
        'max-h-[92vh] w-[calc(100%-1.5rem)] overflow-hidden rounded-2xl border border-slate-200 p-0 shadow-xl backdrop:bg-slate-900/40',
        size === 'sm' && 'max-w-md',
        size === 'md' && 'max-w-xl',
        size === 'lg' && 'max-w-3xl',
        size === 'xl' && 'max-w-5xl'
      )}
    >
      <div className="flex max-h-[92vh] flex-col">
        <header className="flex items-start justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <div>
            <h2 id={titleId} className="text-base font-semibold text-slate-900">
              {title}
            </h2>
            {description ? (
              <p id={descriptionId} className="mt-0.5 text-sm text-slate-500">
                {description}
              </p>
            ) : null}
          </div>
          <button type="button" onClick={onClose} className="rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700" aria-label="Cerrar">
            ✕
          </button>
        </header>
        <div className="flex-1 overflow-y-auto px-5 py-4">{children}</div>
        {footer ? <footer className="flex flex-wrap justify-end gap-2 border-t border-slate-100 bg-slate-50 px-5 py-3">{footer}</footer> : null}
      </div>
    </dialog>
  )
}

export function ConfirmDialog({ open, title, message, confirmLabel = 'Confirmar', danger, loading, onConfirm, onClose, children }: { open: boolean; title: string; message?: React.ReactNode; confirmLabel?: string; danger?: boolean; loading?: boolean; onConfirm: () => void; onClose: () => void; children?: React.ReactNode }) {
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      size="sm"
      footer={
        <>
          <Button onClick={onClose}>Cancelar</Button>
          <Button variant={danger ? 'danger' : 'primary'} loading={loading} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      {message ? <div className="text-sm text-slate-700">{message}</div> : null}
      {children}
    </Modal>
  )
}

export function KeyValue({ items, className }: { items: { label: React.ReactNode; value: React.ReactNode; hidden?: boolean }[]; className?: string }) {
  return (
    <dl className={cn('grid grid-cols-1 gap-x-4 gap-y-2 text-sm sm:grid-cols-2', className)}>
      {items
        .filter((i) => !i.hidden)
        .map((i, idx) => (
          <div key={idx} className="min-w-0">
            <dt className="text-xs text-slate-500">{i.label}</dt>
            <dd className="truncate text-slate-800">{i.value}</dd>
          </div>
        ))}
    </dl>
  )
}

export function SearchInput({ value, onChange, placeholder = 'Buscar…', className, autoFocus, inputRef, onEnter }: { value: string; onChange: (v: string) => void; placeholder?: string; className?: string; autoFocus?: boolean; inputRef?: React.Ref<HTMLInputElement>; onEnter?: () => void }) {
  return (
    <div className={cn('relative', className)}>
      <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-slate-400" aria-hidden="true">
        ⌕
      </span>
      <Input
        ref={inputRef}
        type="search"
        value={value}
        autoFocus={autoFocus}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' && onEnter) {
            e.preventDefault()
            onEnter()
          }
        }}
        placeholder={placeholder}
        className="pl-8"
        aria-label={placeholder}
      />
    </div>
  )
}
