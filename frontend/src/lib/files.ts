import { fetchBlob } from '../api/http'
import { toast } from 'sonner'
import { errorMessage } from '../api/http'

/** Opens an authenticated PDF in a new tab (or downloads when the popup is blocked). */
export async function openPdf(path: string, params?: object) {
  const tab = window.open('', '_blank')
  try {
    const { blobUrl } = await fetchBlob(path, params)
    if (tab) tab.location.href = blobUrl
    else window.location.href = blobUrl
    setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000)
  } catch (err) {
    tab?.close()
    toast.error('No se pudo abrir el documento', { description: errorMessage(err) })
  }
}

/** Prints an authenticated PDF using a hidden iframe (receipts, labels, tickets). */
export async function printPdf(path: string) {
  try {
    const { blobUrl } = await fetchBlob(path)
    const frame = document.createElement('iframe')
    frame.style.position = 'fixed'
    frame.style.right = '0'
    frame.style.bottom = '0'
    frame.style.width = '0'
    frame.style.height = '0'
    frame.style.border = '0'
    frame.src = blobUrl
    frame.onload = () => {
      try {
        frame.contentWindow?.focus()
        frame.contentWindow?.print()
      } catch {
        window.open(blobUrl, '_blank')
      }
      setTimeout(() => {
        frame.remove()
        URL.revokeObjectURL(blobUrl)
      }, 60_000)
    }
    document.body.appendChild(frame)
  } catch (err) {
    toast.error('No se pudo imprimir', { description: errorMessage(err) })
  }
}

export async function downloadFile(path: string, fallbackName: string, params?: object) {
  try {
    const { blobUrl, fileName } = await fetchBlob(path, params)
    const a = document.createElement('a')
    a.href = blobUrl
    a.download = fileName ?? fallbackName
    document.body.appendChild(a)
    a.click()
    a.remove()
    setTimeout(() => URL.revokeObjectURL(blobUrl), 30_000)
  } catch (err) {
    toast.error('No se pudo descargar', { description: errorMessage(err) })
  }
}

export function readFileAsBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => {
      const result = String(reader.result ?? '')
      resolve(result.includes(',') ? result.split(',')[1]! : result)
    }
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}
