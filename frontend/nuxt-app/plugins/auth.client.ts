import { isPublicCuradoriaPath } from '~/utils/curadoriaPublicRoutes'

export default defineNuxtPlugin(() => {
  if (isPublicCuradoriaPath(useRoute().path)) return
  const auth = useAuthStore()
  auth.loadFromStorage()
})
