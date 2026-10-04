import type { Lang } from './lang'

// Text around the contact email and the seller name. Shared with scripts/prerender.mjs, which turns the
// pre-rendered copies into templates the web server fills in from CONTACT_EMAIL and LEGAL_NAME.

/** Link text when no contact email is configured (it points to techxto.ar). */
export function contactFallback(lang: Lang): string {
  return lang === 'en' ? 'the channels listed at techxto.ar' : 'los canales publicados en techxto.ar'
}

/** Sentence that names the seller in the Terms (left out when LEGAL_NAME is not set). */
export function sellerSentence(lang: Lang, name: string): string {
  return lang === 'en' ? ` The service is provided by ${name}.` : ` El servicio lo presta ${name}.`
}
