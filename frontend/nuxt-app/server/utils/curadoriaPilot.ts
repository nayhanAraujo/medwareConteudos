import { timingSafeEqual } from 'node:crypto'

// Private pin is an operator-configured SHA-256 of the exact, previously verified group chat ID.
// This is NOT a credential, an identity assertion or RSC/Graph authorization.
export async function handleCuradoriaPilot(request: Request, configuredPin: unknown): Promise<Response> {
  const reply = (status: number, allowed = false) => Response.json({ allowed }, {
    status, headers: { 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' }
  })
  if (request.method !== 'POST') return reply(405)
  if (new URL(request.url).search) return reply(400)
  if (request.headers.get('content-type')?.split(';')[0]?.trim().toLowerCase() !== 'application/json') return reply(415)
  const size = request.headers.get('content-length')
  if (size && (!/^\d+$/.test(size) || Number(size) > 256)) return reply(413)
  const reader = request.body?.getReader()
  if (!reader) return reply(400)
  let timer: ReturnType<typeof setTimeout> | undefined
  try {
    const timeout = new Promise<never>((_, reject) => { timer = setTimeout(() => reject(new Error('timeout')), 5000) })
    const chunks: Uint8Array[] = []
    let length = 0
    while (true) {
      const next = await Promise.race([reader.read(), timeout])
      if (next.done) break
      length += next.value.byteLength
      if (length > 256) return reply(413)
      chunks.push(next.value)
    }
    const body: unknown = JSON.parse(Buffer.concat(chunks).toString('utf8'))
    if (!body || typeof body !== 'object' || Array.isArray(body) || Object.keys(body).length !== 1 ||
        !('chatSha256' in body) || typeof body.chatSha256 !== 'string' || !/^[a-f0-9]{64}$/.test(body.chatSha256)) return reply(400)
    if (typeof configuredPin !== 'string' || !/^sha256:[a-f0-9]{64}$/.test(configuredPin)) return reply(503)
    return reply(200, timingSafeEqual(Buffer.from(body.chatSha256, 'hex'), Buffer.from(configuredPin.slice(7), 'hex')))
  } catch { return reply(400) } finally {
    clearTimeout(timer)
    void reader.cancel().catch(() => {})
  }
}
