export function useApi() {
  const config = useRuntimeConfig()
  const auth = useAuthStore()
  const route = useRoute()
  const swal = useSwal()

  const isAuthRedirecting = useState<boolean>('auth-redirecting', () => false)

  function apiUrl(path: string) {
    const base = import.meta.client ? config.public.apiBase : config.apiServerBase
    return `${String(base || '').replace(/\/$/, '')}${path}`
  }

  async function handleUnauthorized() {
    auth.logout()
    if (!import.meta.client || isAuthRedirecting.value) return
    isAuthRedirecting.value = true
    await swal.error('Sessao expirada', 'Sua sessao expirou. Faca login novamente.')
    const redirect = route.fullPath && route.fullPath !== '/login' ? route.fullPath : '/'
    await navigateTo({ path: '/login', query: { redirect } })
    isAuthRedirecting.value = false
  }

  async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string> || {})
    }
    if (auth.token) {
      headers.Authorization = `Bearer ${auth.token}`
    }
    const res = await fetch(apiUrl(path), {
      ...options,
      headers
    })
    const data = await res.json().catch(() => ({}))
    if (res.status === 401 && !path.includes('/api/web/auth/login')) {
      await handleUnauthorized()
    }
    if (!res.ok) {
      throw new Error(data?.message || data?.error || `Erro HTTP ${res.status}`)
    }
    return data as T
  }

  async function requestForm<T>(path: string, form: FormData, method = 'POST'): Promise<T> {
    const headers: Record<string, string> = {}
    if (auth.token) headers.Authorization = `Bearer ${auth.token}`
    const res = await fetch(apiUrl(path), { method, headers, body: form })
    const data = await res.json().catch(() => ({}))
    if (res.status === 401 && !path.includes('/api/web/auth/login')) {
      await handleUnauthorized()
    }
    if (!res.ok) throw new Error(data?.message || data?.error || `Erro HTTP ${res.status}`)
    return data as T
  }

  async function getBlob(path: string): Promise<Blob> {
    const headers: Record<string, string> = {}
    if (auth.token) headers.Authorization = `Bearer ${auth.token}`
    const res = await fetch(apiUrl(path), { headers })
    if (res.status === 401 && !path.includes('/api/web/auth/login')) {
      await handleUnauthorized()
    }
    if (!res.ok) throw new Error(`Erro HTTP ${res.status}`)
    return res.blob()
  }

  return {
    request,
    get: <T>(path: string) => request<T>(path),
    post: <T>(path: string, body?: unknown) =>
      request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
    put: <T>(path: string, body?: unknown) =>
      request<T>(path, { method: 'PUT', body: body ? JSON.stringify(body) : undefined }),
    del: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
    postForm: <T>(path: string, form: FormData) => requestForm<T>(path, form, 'POST'),
    putForm: <T>(path: string, form: FormData) => requestForm<T>(path, form, 'PUT'),
    getBlob
  }
}
