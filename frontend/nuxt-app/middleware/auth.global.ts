import { contentRouteDomain } from '~/utils/contentNavigation'

export default defineNuxtRouteMiddleware((to) => {
  const auth = useAuthStore()
  auth.loadFromStorage()

  const publicPaths = ['/login', '/forgot-password', '/reset-password', '/apiconteudos/docs']
  const isPublicApproval = /^\/scripts\/aprovar\/[^/]+$/.test(to.path)
  if (publicPaths.includes(to.path) || isPublicApproval) {
    if (auth.isAuthenticated && to.path === '/login') {
      return navigateTo('/')
    }
    return
  }

  if (!auth.isAuthenticated) {
    return navigateTo({ path: '/login', query: { redirect: to.fullPath } })
  }

  const contentDomain = contentRouteDomain(to.path)
  if (contentDomain && !auth.can(contentDomain, 'visualizar')) {
    return abortNavigation(createError({ statusCode: 403, statusMessage: 'Sem permissão para acessar este módulo.' }))
  }

  if (to.path === '/studio' || to.path.startsWith('/studio/')) {
    const action = to.meta.studioAction as string | undefined
    if (!auth.can('studio', 'visualizar') || (action && !auth.can('studio', action))) {
      return abortNavigation(createError({ statusCode: 403, statusMessage: 'Sem permissão para acessar o Studio.' }))
    }
  }
  if ((to.path === '/biblioteca/orcamentos' || to.path.startsWith('/biblioteca/orcamentos/')) && !auth.can('orcamentos', 'visualizar')) {
    return abortNavigation(createError({ statusCode: 403, statusMessage: 'Sem permissão para acessar a Central de Orçamentos.' }))
  }
})
