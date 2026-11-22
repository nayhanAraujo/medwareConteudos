import { isPublicCuradoriaPath } from '~/utils/curadoriaPublicRoutes'
import { isPublicChatfastPath } from '~/utils/chatfastPublicRoutes'

export default defineNuxtPlugin(() => {
  if (isPublicCuradoriaPath(useRoute().path) || isPublicChatfastPath(useRoute().path)) return
  const auth = useAuthStore()
  auth.loadFromStorage()
})
