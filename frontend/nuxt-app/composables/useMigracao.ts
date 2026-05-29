const FLASK_BASE = 'http://localhost:5000'

export function useMigracao() {
  const swal = useSwal()
  const router = useRouter()

  function emMigracao() {
    swal.toast('Funcionalidade em migração para a nova stack.', 'info')
  }

  function abrirFlask(path: string) {
    if (import.meta.client) {
      window.open(`${FLASK_BASE}${path}`, '_blank')
    }
  }

  async function irOuMigracao(path: string | undefined, flaskPath?: string) {
    if (path) {
      await router.push(path)
      return
    }
    if (flaskPath) {
      abrirFlask(flaskPath)
      return
    }
    emMigracao()
  }

  return { emMigracao, abrirFlask, irOuMigracao, FLASK_BASE }
}
