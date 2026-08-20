import type { ConversionFormat, ConversionResponse } from '~/types/conversion'

export function normalizeConversionFormat(format: unknown, fallback: ConversionFormat = 'html'): ConversionFormat {
  if (format === 'modoTexto' || format === 'ModoTexto' || format === 1) return 'modoTexto'
  if (format === 'html' || format === 'Html' || format === 0) return 'html'
  return fallback
}

export function looksLikeModoTexto(content: string): boolean {
  const trimmed = content.trim()
  if (!trimmed) return false
  if (/<div[\s>]/i.test(trimmed) || /<style[\s>]/i.test(trimmed)) return false
  return /^\[[^\]]+\]/m.test(trimmed)
}

export function looksLikeHtml(content: string): boolean {
  const trimmed = content.trim()
  return /<div[\s>]/i.test(trimmed) || /<style[\s>]/i.test(trimmed) || trimmed.includes('containerHtml')
}

export function resolveConversionContent(
  requestedFormat: ConversionFormat,
  result: ConversionResponse
): { format: ConversionFormat; html: string; text: string; apiMismatch: boolean } {
  const apiFormat = result.format != null
    ? normalizeConversionFormat(result.format, requestedFormat)
    : requestedFormat

  if (requestedFormat === 'modoTexto') {
    let text = (result.text ?? '').trim()
    if (!text && looksLikeModoTexto(result.html ?? '')) {
      text = (result.html ?? '').trim()
    }

    const receivedHtml = (result.html ?? '').trim()
    const apiMismatch =
      apiFormat !== 'modoTexto'
      || looksLikeHtml(text)
      || (!text && looksLikeHtml(receivedHtml))

    return {
      format: 'modoTexto',
      html: '',
      text,
      apiMismatch
    }
  }

  return {
    format: 'html',
    html: (result.html ?? '').trim(),
    text: '',
    apiMismatch: apiFormat !== 'html'
  }
}
