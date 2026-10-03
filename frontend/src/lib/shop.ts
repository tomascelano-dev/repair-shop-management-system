import { useQuery } from '@tanstack/react-query'
import { settingsApi } from '../api/endpoints'

/** Settings of the current branch (readable by every staff member; edited in Configuración). */
export function useShopSettings() {
  return useQuery({ queryKey: ['settings'], queryFn: settingsApi.get, staleTime: 5 * 60_000 })
}

export function useDefaultCurrency(): string {
  return useShopSettings().data?.defaultCurrency ?? 'ARS'
}

export const CURRENCIES = ['ARS', 'USD']
