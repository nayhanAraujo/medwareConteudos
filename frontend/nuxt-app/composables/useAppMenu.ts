export interface AppMenuItem {
  label: string
  path: string
  icon: string
  exact?: boolean
}

export function useAppMenu(): AppMenuItem[] {
  const auth = useAuthStore()
  return [
    { label: 'Dashboards', path: '/', icon: 'speedometer2', exact: true },
    { label: 'Visualizar Conteúdos', path: '/conteudos', icon: 'boxes' },
    { label: 'Biblioteca', path: '/biblioteca', icon: 'journal-bookmark' },
    ...(auth.can('avaliacoes', 'visualizar') ? [{ label: 'Central de Avaliações', path: '/avaliacoes', icon: 'chat-square-heart' }] : [])
  ]
}

export function isMenuActive(routePath: string, item: AppMenuItem): boolean {
  if (item.exact) return routePath === item.path
  if (item.path === '/') return routePath === '/'
  return routePath === item.path || routePath.startsWith(`${item.path}/`)
}
