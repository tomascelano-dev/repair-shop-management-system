// Paddle.js (Paddle Billing v2) loader. The API creates a transaction and sends the browser to the billing page
// with ?_ptxn=txn_...; initializing Paddle.js on that page opens the checkout for that transaction.

interface PaddleEvent {
  name?: string
}

interface PaddleGlobal {
  Environment: { set: (env: 'sandbox' | 'production') => void }
  Initialize: (options: { token: string; eventCallback?: (event: PaddleEvent) => void }) => void
}

declare global {
  interface Window {
    Paddle?: PaddleGlobal
  }
}

const SCRIPT = 'https://cdn.paddle.com/paddle/v2/paddle.js'
let loading: Promise<PaddleGlobal> | null = null

function load(): Promise<PaddleGlobal> {
  if (window.Paddle) return Promise.resolve(window.Paddle)
  if (!loading) {
    loading = new Promise((resolve, reject) => {
      const script = document.createElement('script')
      script.src = SCRIPT
      script.async = true
      script.onload = () => (window.Paddle ? resolve(window.Paddle) : reject(new Error('Paddle.js no se cargó.')))
      script.onerror = () => {
        loading = null
        reject(new Error('No se pudo cargar el checkout de Paddle. Revisá la conexión o desactivá el bloqueador de anuncios.'))
      }
      document.head.appendChild(script)
    })
  }
  return loading
}

/** Opens the pending checkout (?_ptxn=) and reports when the payment completes or the overlay closes. */
export async function openPendingPaddleCheckout(
  token: string,
  environment: 'sandbox' | 'production',
  on: { completed: () => void; closed: () => void }
): Promise<void> {
  const paddle = await load()
  if (environment === 'sandbox') paddle.Environment.set('sandbox')
  paddle.Initialize({
    token,
    eventCallback: (event) => {
      if (event.name === 'checkout.completed') on.completed()
      else if (event.name === 'checkout.closed') on.closed()
    },
  })
}
