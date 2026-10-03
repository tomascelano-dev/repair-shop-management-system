import { describe, expect, it } from 'vitest'
import { errorMessage, fieldErrors, toProblem } from './http'

const axiosError = (status: number, data?: unknown, code?: string) => ({ isAxiosError: true, code, message: 'Request failed', response: status ? { status, data } : undefined })

describe('API errors', () => {
  it('uses the ProblemDetails text', () => {
    const err = axiosError(409, { title: 'Conflicto', detail: 'El stock cambió.' })
    expect(errorMessage(err)).toBe('El stock cambió.')
    expect(toProblem(err).status).toBe(409)
  })

  it('prefers the first validation error', () => {
    const err = axiosError(400, { title: 'Datos inválidos', errors: { phone: ['El teléfono es obligatorio.'], email: ['Email inválido.'] } })
    expect(errorMessage(err)).toBe('El teléfono es obligatorio.')
    expect(fieldErrors(err)).toEqual({ phone: 'El teléfono es obligatorio.', email: 'Email inválido.' })
  })

  it('explains rate limiting and network failures', () => {
    expect(toProblem(axiosError(429)).title).toBe('Demasiadas solicitudes')
    expect(errorMessage(axiosError(0, undefined, 'ERR_NETWORK'))).toMatch(/No se pudo conectar/)
  })
})
