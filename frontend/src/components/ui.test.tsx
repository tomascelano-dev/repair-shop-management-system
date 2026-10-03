import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Field, Input, Modal, Pagination } from './ui'

describe('Field', () => {
  it('links the label, the control and the error message', () => {
    render(
      <Field label="Teléfono" error="Es obligatorio">
        <Input />
      </Field>
    )
    const input = screen.getByLabelText('Teléfono')
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAccessibleDescription('Es obligatorio')
  })
})

describe('Pagination', () => {
  it('shows the range and moves between pages', async () => {
    const onChange = vi.fn()
    render(<Pagination skip={25} take={25} total={60} onChange={onChange} />)
    expect(screen.getByText('26–50 de 60')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Siguiente/ }))
    expect(onChange).toHaveBeenCalledWith(50)
    await userEvent.click(screen.getByRole('button', { name: /Anterior/ }))
    expect(onChange).toHaveBeenCalledWith(0)
  })

  it('only shows the count when everything fits in one page', () => {
    render(<Pagination skip={0} take={25} total={3} onChange={() => undefined} />)
    expect(screen.getByText('3 resultados')).toBeInTheDocument()
  })
})

describe('Modal', () => {
  it('renders its content and closes from the close button', async () => {
    const onClose = vi.fn()
    render(
      <Modal open onClose={onClose} title="Cobrar">
        <p>Contenido</p>
      </Modal>
    )
    expect(screen.getByText('Contenido')).toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: 'Cobrar' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Cerrar' }))
    expect(onClose).toHaveBeenCalled()
  })

  it('renders nothing when closed', () => {
    render(
      <Modal open={false} onClose={() => undefined} title="Oculto">
        <p>Nada</p>
      </Modal>
    )
    expect(screen.queryByText('Nada')).not.toBeInTheDocument()
  })
})
