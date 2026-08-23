export function mediaUrl(path?: string | null): string {
  if (!path) return ''
  const trimmed = String(path).trim()
  if (!trimmed) return ''
  if (/^https?:\/\//i.test(trimmed)) return trimmed

  // Caminhos absolutos Windows legados → tenta extrair /static/uploads/...
  const normalized = trimmed.replace(/\\/g, '/')
  const staticIdx = normalized.toLowerCase().indexOf('/static/uploads/')
  if (staticIdx >= 0) return normalized.slice(staticIdx)

  const uploadsIdx = normalized.toLowerCase().indexOf('/uploads/')
  if (uploadsIdx >= 0) return `/static${normalized.slice(uploadsIdx)}`

  if (normalized.startsWith('/static/')) return normalized
  if (normalized.startsWith('static/')) return `/${normalized}`
  if (normalized.startsWith('/uploads/')) return `/static${normalized}`
  if (normalized.startsWith('uploads/')) return `/static/${normalized}`

  // Relativo sem prefixo
  if (!normalized.startsWith('/')) return `/static/uploads/${normalized}`
  return normalized
}
