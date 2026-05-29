export default defineNuxtRouteMiddleware(async () => {
  const auth = useAuthStore()
  if (auth.user?.role !== 'admin') {
    if (import.meta.client) {
      const swal = useSwal()
      await swal.error('Acesso negado', 'Apenas administradores podem acessar a Biblioteca.')
    }
    return navigateTo('/')
  }
})
