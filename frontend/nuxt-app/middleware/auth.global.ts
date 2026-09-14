export default defineNuxtRouteMiddleware((to) => {
  const auth = useAuthStore()
  auth.loadFromStorage()

  const publicPaths = ['/login', '/forgot-password', '/apiconteudos/docs']
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
})
