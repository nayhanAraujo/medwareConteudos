import { readFile, stat } from 'node:fs/promises'
import { createHash } from 'node:crypto'
import { pathToFileURL } from 'node:url'

export function endpoint(base, kind, allowLocalHttp = false) {
  const url = new URL(base)
  if (url.username || url.password || url.search || url.hash || !['respostas', 'relatorios'].includes(kind)) throw new Error('Destino inválido.')
  const local = ['127.0.0.1', 'localhost', '[::1]'].includes(url.hostname)
  if (url.protocol !== 'https:' && !(allowLocalHttp && local && url.protocol === 'http:')) throw new Error('Use HTTPS. HTTP somente local, com autorização explícita.')
  url.pathname = `${url.pathname.replace(/\/$/, '')}/api/integracoes/chatfast/v1/${kind}`
  return url
}
export async function publish(payload, { base, key, kind = 'relatorios', send = false, allowLocalHttp = false, fetchImpl = globalThis.fetch, pause = ms => new Promise(resolve => setTimeout(resolve, ms)) }) {
  const url = endpoint(base, kind, allowLocalHttp)
  const body = JSON.stringify(payload)
  if (Buffer.byteLength(body) > 1_048_576 || !['1.0', '1.1'].includes(payload.schemaVersion) || (kind === 'respostas' && payload.schemaVersion !== '1.0')) throw new Error('Contrato ou tamanho inválido.')
  if (kind === 'relatorios' && (!payload.batchId || !['diario', 'semanal'].includes(payload.type) || !payload.metrics)) throw new Error('Relatório inválido.')
  if (kind === 'respostas' && (!payload.flowId || !payload.runId || !payload.response)) throw new Error('Resposta inválida.')
  const digest = createHash('sha256').update(body).digest('hex')
  if (!send) return { dryRun: true, bytes: Buffer.byteLength(body), sha256: digest }
  if (typeof key !== 'string' || key.length < 32 || key.length > 512 || /[\r\n]/.test(key)) throw new Error('Configure a chave privada do produtor.')
  for (let attempt = 0; attempt < 3; attempt++) {
    let response
    try { response = await fetchImpl(url, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Chatfast-Key': key }, body, signal: AbortSignal.timeout(20_000), redirect: 'error' }) }
    catch { if (attempt === 2) throw new Error('Falha de transporte. Preserve o mesmo lote para repetir.'); await pause(1000 * 2 ** attempt); continue }
    if (response.ok) {
      const result = await response.json()
      return { success: result.success === true, duplicate: result.duplicate === true }
    }
    // Never print response bodies: proxies or servers could return confidential content.
    if (attempt === 2 || (response.status < 500 && ![408, 429].includes(response.status))) throw new Error(`API recusou a solicitação (HTTP ${response.status}). Preserve o lote; não altere sua identidade.`)
    await response.body?.cancel()
    await pause(1000 * 2 ** attempt)
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const args = process.argv.slice(2)
  try {
    const at = args.indexOf('--file'); const kind = args.includes('--response') ? 'respostas' : 'relatorios'
    if (at < 0 || !args[at + 1]) throw new Error('Informe --file arquivo.json. Sem --send, apenas valida; nunca envia.')
    if ((await stat(args[at + 1])).size > 1_048_576) throw new Error('Arquivo excede 1 MB.')
    const bytes = await readFile(args[at + 1]); if (bytes.length > 1_048_576) throw new Error('Arquivo excede 1 MB.')
    const result = await publish(JSON.parse(bytes.toString('utf8')), { base: process.env.CHATFAST_API_BASE, key: process.env[kind === 'relatorios' ? 'CHATFAST_REPORTS_KEY' : 'CHATFAST_RESPONSES_KEY'], kind, send: args.includes('--send'), allowLocalHttp: args.includes('--allow-local-http') })
    console.log(JSON.stringify(result))
  } catch (e) { console.error(e instanceof SyntaxError ? 'JSON inválido.' : e.message === 'Invalid URL' ? 'Configure CHATFAST_API_BASE.' : e.message); process.exitCode = 1 }
}
