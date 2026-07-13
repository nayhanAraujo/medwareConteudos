/** Lê arquivo de relatório (XML/JSON) tentando encodings do legado Medware. */
export async function readRelatorioFileText(file: File): Promise<string> {
  const buffer = await file.arrayBuffer()
  const bytes = new Uint8Array(buffer)

  // Detecta declaração no trecho inicial (lido como latin1 só para o header)
  const headLatin1 = decodeBytes(bytes.slice(0, 256), 'iso-8859-1')
  const declared = detectXmlEncoding(headLatin1)

  const candidates: string[] = []
  if (declared) candidates.push(declared)
  candidates.push('utf-8', 'iso-8859-1', 'windows-1252')

  let best = ''
  let bestScore = Number.NEGATIVE_INFINITY
  for (const enc of candidates) {
    try {
      const text = decodeBytes(bytes, enc)
      const score = scoreDecodedText(text)
      if (score > bestScore) {
        bestScore = score
        best = text
      }
      if (score > 0 && !text.includes('\uFFFD')) {
        return text
      }
    } catch {
      /* tenta próximo */
    }
  }
  return best || (await file.text())
}

function detectXmlEncoding(content: string): string | null {
  const m = content.match(/encoding\s*=\s*["']([^"']+)["']/i)
  if (!m) return null
  const raw = m[1].toLowerCase()
  const map: Record<string, string> = {
    'iso-8859-1': 'iso-8859-1',
    latin1: 'iso-8859-1',
    'utf-8': 'utf-8',
    utf8: 'utf-8',
    'windows-1252': 'windows-1252',
    cp1252: 'windows-1252'
  }
  return map[raw] || raw
}

function decodeBytes(bytes: Uint8Array, encoding: string): string {
  return new TextDecoder(encoding, { fatal: false }).decode(bytes)
}

function scoreDecodedText(text: string): number {
  if (!text?.trim()) return Number.NEGATIVE_INFINITY
  const sample = text.slice(0, 4000)
  let score = 0
  if (sample.includes('\uFFFD')) score -= 100
  if (sample.trimStart().startsWith('<') || sample.trimStart().startsWith('{')) score += 20
  for (const c of sample) {
    if ('áàâãéêíóôõúçÁÀÂÃÉÊÍÓÔÕÚÇ'.includes(c)) score += 3
  }
  return score
}
