// Exact document routes only. Never make the whole /curadoria prefix public.
export function isPublicCuradoriaPath(path: string): boolean {
  return path === '/curadoria/privacidade' || path === '/curadoria/termos'
}
