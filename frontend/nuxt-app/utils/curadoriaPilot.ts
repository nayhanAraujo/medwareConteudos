// Presentation check only, never authentication. The raw SDK identifier stays in memory.
export async function verifyCuradoriaPilotChat(chatId: string): Promise<boolean> {
  if (typeof chatId !== 'string' || !chatId.trim() || chatId.length > 2048) return false
  const abort = new AbortController()
  const timer = setTimeout(() => abort.abort(), 5000)
  try {
    const digest = await globalThis.crypto.subtle.digest('SHA-256', new TextEncoder().encode(chatId))
    const chatSha256 = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('')
    const response = await fetch('/api/curadoria/teams/piloto', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ chatSha256 }), signal: abort.signal,
      credentials: 'omit', cache: 'no-store', redirect: 'error', referrerPolicy: 'no-referrer'
    })
    if (!response.ok) return false
    const result: unknown = await response.json()
    return !!result && typeof result === 'object' && 'allowed' in result && result.allowed === true
  } catch { return false } finally { clearTimeout(timer) }
}
