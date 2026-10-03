import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { inventoryApi } from '../../api/endpoints'
import type { InventoryItem } from '../../api/types'
import { Input, Spinner } from '../../components/ui'
import { cn } from '../../lib/cn'
import { money } from '../../lib/format'
import { useDebounced } from '../../lib/hooks'

/** Type-ahead over the inventory (name, SKU or barcode). Optional "compatible with" filter by device. */
export function ItemPicker({ onSelect, placeholder = 'Buscar repuesto por nombre, SKU o código…', brand, model, sellableOnly, autoFocus }: {
  onSelect: (item: InventoryItem) => void
  placeholder?: string
  brand?: string
  model?: string
  sellableOnly?: boolean
  autoFocus?: boolean
}) {
  const [q, setQ] = useState('')
  const [open, setOpen] = useState(false)
  const [onlyCompatible, setOnlyCompatible] = useState(!!(brand && model))
  const debounced = useDebounced(q.trim(), 250)
  const query = useQuery({
    queryKey: ['inventory', 'picker', debounced, onlyCompatible ? brand : null, onlyCompatible ? model : null, sellableOnly],
    queryFn: () => inventoryApi.search({ q: debounced || undefined, brand: onlyCompatible ? brand : undefined, model: onlyCompatible ? model : undefined, onlySellable: sellableOnly || undefined, take: 10 }),
    enabled: open && (debounced.length >= 2 || onlyCompatible),
  })
  const items = query.data?.items ?? []

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
        aria-label={placeholder}
      />
      {brand && model ? (
        <label className="mt-1 flex items-center gap-1.5 text-xs text-slate-600">
          <input type="checkbox" checked={onlyCompatible} onChange={(e) => setOnlyCompatible(e.target.checked)} />
          Solo compatibles con {brand} {model}
        </label>
      ) : null}
      {open && (debounced.length >= 2 || onlyCompatible) ? (
        <ul className="absolute z-30 mt-1 max-h-72 w-full overflow-y-auto rounded-lg border border-slate-200 bg-white py-1 text-sm shadow-lg">
          {query.isFetching && items.length === 0 ? (
            <li className="flex items-center gap-2 px-3 py-2 text-slate-500">
              <Spinner className="h-4 w-4" /> Buscando…
            </li>
          ) : items.length === 0 ? (
            <li className="px-3 py-2 text-slate-500">Sin resultados.</li>
          ) : (
            items.map((i) => (
              <li key={i.id}>
                <button
                  type="button"
                  className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left hover:bg-brand-50"
                  onMouseDown={(e) => {
                    e.preventDefault()
                    onSelect(i)
                    setQ('')
                    setOpen(false)
                  }}
                >
                  <span className="min-w-0">
                    <span className="block truncate font-medium text-slate-800">{i.name}</span>
                    <span className="text-xs text-slate-500">
                      {i.sku}
                      {i.barcode ? ` · ${i.barcode}` : ''}
                    </span>
                  </span>
                  <span className="shrink-0 text-right text-xs">
                    <span className={cn('block', i.trackStock && i.availableQuantity <= 0 ? 'text-rose-600' : 'text-slate-600')}>
                      {i.trackStock ? `${i.availableQuantity} disp.` : 'Sin control de stock'}
                    </span>
                    {i.salePrice !== null && i.salePrice !== undefined ? <span className="text-slate-500">{money(i.salePrice, i.salePriceCurrency ?? 'ARS')}</span> : null}
                  </span>
                </button>
              </li>
            ))
          )}
        </ul>
      ) : null}
    </div>
  )
}
