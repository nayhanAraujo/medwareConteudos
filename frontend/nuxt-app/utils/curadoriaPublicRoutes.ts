// Exact public pages only. Never make the whole /curadoria prefix public.
export function isTeamsCuradoriaPath(path: string): boolean {
  return path === '/curadoria/teams/configurar' || path === '/curadoria/teams/aba'
}

export function isPublicCuradoriaPath(path: string): boolean {
  return path === '/curadoria/privacidade' || path === '/curadoria/termos' || isTeamsCuradoriaPath(path)
}
