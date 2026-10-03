import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import type { CashSession, PosCatalogItem, Sale } from '../../api/types'
import { cashApi, salesApi } from '../../api/endpoints'
import { PosPage } from './PosPage'

vi.mock('../../api/endpoints', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/endpoints')>()
  return {
    ...actual,
    salesApi: { ...actual.salesApi, catalog: vi.fn(), create: vi.fn() },
    cashApi: { ...actual.cashApi, current: vi.fn() },
    inventoryApi: { ...actual.inventoryApi, byCode: vi.fn().mockRejectedValue(new Error('not found')) },
    settingsApi: { ...actual.settingsApi, get: vi.fn().mockResolvedValue({ defaultCurrency: 'ARS', requireOpenCashSession: true }) },
  }
})

const catalog: PosCatalogItem[] = [
  { id: 'a', sku: 'TEMP-13', barcode: '7790001', name: 'Templado iPhone 13', category: 'Accesorios', salePrice: 5000, currency: 'ARS', trackStock: true, available: 10 },
  { id: 'b', sku: 'CARG-20', barcode: '7790002', name: 'Cargador 20W', category: 'Cargadores', salePrice: 12000, currency: 'ARS', trackStock: true, available: 1 },
]

const openSession = { id: 'cash1', number: 1, status: 'Open', currency: 'ARS', openingCash: 0, openedByUserId: 'u', openedAtUtc: '2026-10-03T12:00:00Z', summary: [] } as CashSession

function sale(partial: Partial<Sale>): Sale {
  return {
    id: 's1',
    number: 1,
    code: 'V-000001',
    status: 'Completed',
    currency: 'ARS',
    subtotal: 0,
    discountAmount: 0,
    total: 0,
    paidAmount: 0,
    changeAmount: 0,
    refundedAmount: 0,
    createdByUserId: 'u',
    createdAtUtc: '2026-10-03T12:00:00Z',
    lines: [],
    payments: [],
    refunds: [],
    ...partial,
  }
}

function renderPos() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <PosPage />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

describe('PosPage', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.mocked(salesApi.catalog).mockResolvedValue(catalog)
    vi.mocked(cashApi.current).mockResolvedValue(openSession)
    vi.mocked(salesApi.create).mockReset()
  })

  it('sells several items, paying cash with change', async () => {
    const user = userEvent.setup()
    vi.mocked(salesApi.create).mockResolvedValue(sale({ total: 22000, paidAmount: 25000, changeAmount: 3000 }))
    renderPos()

    await user.click(await screen.findByRole('button', { name: /Templado iPhone 13/ }))
    await user.click(screen.getByRole('button', { name: /Templado iPhone 13/ }))
    await user.click(screen.getByRole('button', { name: /Cargador 20W/ }))
    expect(screen.getByText(/22\.000,00/, { selector: 'span.text-3xl' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Cobrar (F4)' }))
    const amount = await screen.findByLabelText('Monto')
    await user.clear(amount)
    await user.type(amount, '25000')
    expect(screen.getByText('Vuelto')).toBeInTheDocument()
    expect(screen.getByText(/3\.000,00/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Confirmar cobro' }))
    await waitFor(() => expect(salesApi.create).toHaveBeenCalledTimes(1))
    const [body, key] = vi.mocked(salesApi.create).mock.calls[0]!
    expect(body.lines).toEqual([
      { inventoryItemId: 'a', quantity: 2, unitPrice: 5000, discountAmount: 0 },
      { inventoryItemId: 'b', quantity: 1, unitPrice: 12000, discountAmount: 0 },
    ])
    expect(body.payments).toEqual([{ method: 'Cash', amount: 25000, reference: null }])
    expect(body.discountAmount).toBe(0)
    expect(key).toEqual(expect.any(String))

    expect(await screen.findByText('Venta V-000001 registrada')).toBeInTheDocument()
  })

  it('adds the exact item when a barcode is scanned', async () => {
    const user = userEvent.setup()
    renderPos()
    await screen.findByRole('button', { name: /Cargador 20W/ })
    const search = screen.getByRole('searchbox')
    await user.type(search, '7790001{Enter}')
    expect(await screen.findByRole('list', { name: 'Carrito' })).toHaveTextContent('Templado iPhone 13')
    expect(search).toHaveValue('')
  })

  it('applies a global percentage discount', async () => {
    const user = userEvent.setup()
    renderPos()
    await user.click(await screen.findByRole('button', { name: /Cargador 20W/ }))
    await user.selectOptions(screen.getByLabelText('Tipo de descuento'), 'percent')
    await user.type(screen.getByLabelText('Descuento'), '10')
    expect(screen.getByText(/10\.800,00/, { selector: 'span.text-3xl' })).toBeInTheDocument()
  })

  it('only lets cash give change', async () => {
    const user = userEvent.setup()
    renderPos()
    await user.click(await screen.findByRole('button', { name: /Cargador 20W/ }))
    await user.click(screen.getByRole('button', { name: 'Cobrar (F4)' }))
    await user.selectOptions(await screen.findByLabelText('Medio'), 'Card')
    const amount = screen.getByLabelText('Monto')
    await user.clear(amount)
    await user.type(amount, '15000')
    expect(screen.getByText(/Solo el efectivo puede dar vuelto/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Confirmar cobro' })).toBeDisabled()
  })

  it('asks to open the register before charging', async () => {
    const user = userEvent.setup()
    vi.mocked(cashApi.current).mockResolvedValue(null)
    renderPos()
    expect(await screen.findByText('La caja está cerrada')).toBeInTheDocument()
    await user.click(await screen.findByRole('button', { name: /Cargador 20W/ }))
    await user.click(screen.getByRole('button', { name: 'Cobrar (F4)' }))
    await user.selectOptions(await screen.findByLabelText('Medio'), 'Card')
    expect(screen.getByRole('button', { name: 'Confirmar cobro' })).toBeDisabled()
    expect(salesApi.create).not.toHaveBeenCalled()
  })

  it('holds a sale and resumes it later', async () => {
    const user = userEvent.setup()
    renderPos()
    await user.click(await screen.findByRole('button', { name: /Templado iPhone 13/ }))
    await user.click(screen.getByRole('button', { name: 'En espera' }))
    expect(screen.getByText(/Escaneá un producto/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Retomar' }))
    expect(screen.getByRole('list', { name: 'Carrito' })).toHaveTextContent('Templado iPhone 13')
  })
})
