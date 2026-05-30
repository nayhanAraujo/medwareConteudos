const FLASK_BASE = 'http://localhost:5000'

export function useMigracao() {
  const swal = useSwal()
  const router = useRouter()

  function hardCutNuxtPath(flaskPath?: string) {
    if (!flaskPath) return null
    const normalized = flaskPath.trim().toLowerCase()
    if (
      normalized === '/referencias' ||
      normalized === '/referencias/' ||
      normalized === '/referencias/visualizar_referencias'
    ) {
      return '/referencias'
    }
    if (
      normalized === '/referencias/nova' ||
      normalized === '/referencias/nova_referencia'
    ) {
      return '/referencias/nova'
    }
    if (normalized.startsWith('/variaveis/referencias_normalidades')) {
      return '/variaveis/referencias-normalidades'
    }
    if (normalized.startsWith('/variaveis/visualizar_variaveis')) {
      return '/variaveis'
    }
    if (
      normalized === '/variaveis/nova_variavel' ||
      normalized === '/variaveis/nova'
    ) {
      return '/variaveis/nova'
    }
    const editMatch = normalized.match(/^\/variaveis\/editar_variavel\/(\d+)/)
    if (editMatch) return `/variaveis/${editMatch[1]}/editar`
    if (normalized.startsWith('/autores/vincular_autores')) {
      return '/referencias'
    }
    return null
  }

  function emMigracao() {
    swal.toast('Funcionalidade em migração para a nova stack.', 'info')
  }

  function abrirFlask(path: string) {
    if (import.meta.client) {
      window.open(`${FLASK_BASE}${path}`, '_blank')
    }
  }

  function navegarFlask(path: string) {
    if (import.meta.client) {
      window.location.href = `${FLASK_BASE}${path}`
    }
  }

  async function irOuMigracao(path: string | undefined, flaskPath?: string) {
    if (path) {
      await router.push(path)
      return
    }
    const mappedPath = hardCutNuxtPath(flaskPath)
    if (mappedPath) {
      await router.push(mappedPath)
      return
    }
    if (flaskPath) {
      abrirFlask(flaskPath)
      return
    }
    emMigracao()
  }

  return { emMigracao, abrirFlask, navegarFlask, irOuMigracao, FLASK_BASE }
}
