// Exact allowlist: /gestao and future automation routes must remain authenticated.
export function isPublicChatfastPath(path: string): boolean {
  return /^\/automacoes\/chatfast\/(diario|semanal)\/?$/.test(path)
}
