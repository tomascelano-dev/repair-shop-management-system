import { useQuery } from '@tanstack/react-query'
import { settingsApi } from '../api/endpoints'

/** Settings of the current branch (readable by every staff member; edited in Configuración). */
export function useShopSettings() {
  return useQuery({ queryKey: ['settings'], queryFn: settingsApi.get, staleTime: 5 * 60_000 })
}

export function useDefaultCurrency(): string {
  return useShopSettings().data?.defaultCurrency ?? 'ARS'
}

/**
 * Currencies offered in the selectors: the shop's own (MXN for a shop in México, ARS in Argentina...) plus pesos
 * and dollars, which every shop may use.
 */
export function currencyOptions(...current: (string | null | undefined)[]): string[] {
  return [...new Set([...current.filter((c): c is string => !!c), 'ARS', 'USD'])]
}
