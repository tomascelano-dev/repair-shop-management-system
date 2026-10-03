import { describe, expect, it } from 'vitest'
import type { RepairOrder } from '../../api/types'
import { transitionBlocker } from './rules'

const order = (o: Partial<RepairOrder>) => ({ status: 'Diagnosing', isWarrantyClaim: false, hasApprovedQuote: false, qaPassed: false, ...o }) as RepairOrder

describe('transitionBlocker', () => {
  it('requires an approved quote to start the repair', () => {
    expect(transitionBlocker(order({}), 'InProgress')).toMatch(/presupuesto/)
    expect(transitionBlocker(order({}), 'WaitingParts')).toMatch(/presupuesto/)
    expect(transitionBlocker(order({ hasApprovedQuote: true }), 'InProgress')).toBeNull()
  })

  it('lets warranty claims skip the quote', () => {
    expect(transitionBlocker(order({ isWarrantyClaim: true }), 'InProgress')).toBeNull()
  })

  it('requires QA before Ready', () => {
    expect(transitionBlocker(order({ status: 'InProgress' }), 'Ready')).toMatch(/control de calidad/)
    expect(transitionBlocker(order({ status: 'Testing', qaPassed: true }), 'Ready')).toBeNull()
  })

  it('does not block cancellations', () => {
    expect(transitionBlocker(order({}), 'Cancelled')).toBeNull()
  })
})
