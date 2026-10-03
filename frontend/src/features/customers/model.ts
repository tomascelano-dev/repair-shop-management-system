import type { Customer, CustomerInput, Device, DeviceInput } from '../../api/types'

export function emptyCustomer(): CustomerInput {
  return {
    fullName: '',
    phone: '',
    email: '',
    notes: '',
    documentType: 'None',
    documentNumber: '',
    taxCondition: 'ConsumidorFinal',
    address: '',
    tags: '',
    notificationsOptIn: true,
    marketingOptIn: false,
  }
}

export function customerToInput(c: Customer): CustomerInput {
  return {
    fullName: c.fullName,
    phone: c.phone,
    email: c.email ?? '',
    notes: c.notes ?? '',
    documentType: c.documentType,
    documentNumber: c.documentNumber ?? '',
    taxCondition: c.taxCondition,
    address: c.address ?? '',
    tags: c.tags ?? '',
    notificationsOptIn: c.notificationsOptIn,
    marketingOptIn: c.marketingOptIn,
  }
}

export function emptyDevice(): DeviceInput {
  return { brand: '', model: '', label: '', serialNumber: '', imei: '', notes: '' }
}

export function deviceToInput(d: Device): DeviceInput {
  return { brand: d.brand, model: d.model, label: d.label ?? '', serialNumber: d.serialNumber ?? '', imei: d.imei ?? '', notes: d.notes ?? '' }
}

/** Luhn check for the 15-digit IMEI (same rule as the API). */
export function isValidImei(value: string) {
  const d = value.replace(/\D/g, '')
  if (d.length !== 15) return false
  let sum = 0
  for (let i = 0; i < 15; i++) {
    let n = Number(d[i])
    if (i % 2 === 1) {
      n *= 2
      if (n > 9) n -= 9
    }
    sum += n
  }
  return sum % 10 === 0
}
