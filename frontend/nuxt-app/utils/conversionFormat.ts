import type { ConversionFormat, ConversionResponse } from '~/types/conversion'

export function normalizeConversionFormat(format: unknown, fallback: ConversionFormat = 'html'): ConversionFormat {
  if (format === 'modoTexto' || format === 'ModoTexto' || format === 1) return 'modoTexto'
  if (format === 'jsonStudio' || format === 'JsonStudio' || format === 2 || format === 'json') return 'jsonStudio'
  if (format === 'html' || format === 'Html' || format === 0) return 'html'
  return fallback
}

export function looksLikeModoTexto(content: string): boolean {
  const trimmed = content.trim()
  if (!trimmed) return false
  if (/<div[\s>]/i.test(trimmed) || /<style[\s>]/i.test(trimmed)) return false
  if (trimmed.startsWith('{') && trimmed.includes('camposScript')) return false
  return /^\[[^\]]+\]/m.test(trimmed)
}

export function looksLikeJsonStudio(content: string): boolean {
  const trimmed = content.trim()
  if (!trimmed.startsWith('{')) return false
  try {
    const parsed = JSON.parse(trimmed) as { camposScript?: unknown }
    return Array.isArray(parsed.camposScript)
  } catch {
    return trimmed.includes('"camposScript"')
  }
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

  if (requestedFormat === 'jsonStudio') {
    let text = (result.text ?? '').trim()
    if (!text && looksLikeJsonStudio(result.html ?? '')) {
      text = (result.html ?? '').trim()
    }

    return {
      format: 'jsonStudio',
      html: '',
      text,
      apiMismatch: apiFormat !== 'jsonStudio' || (!!text && !looksLikeJsonStudio(text))
    }
  }

  return {
    format: 'html',
    html: (result.html ?? '').trim(),
    text: '',
    apiMismatch: apiFormat !== 'html'
  }
}
