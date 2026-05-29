export const SISTEMAS = ['Laudos UX', 'Laudos Flex'] as const
export type SistemaLaudo = (typeof SISTEMAS)[number]

export function useScriptsNav() {
  const router = useRouter()

  function isSistemaValido(s?: string): s is SistemaLaudo {
    return s === 'Laudos UX' || s === 'Laudos Flex'
  }

  function goSistema() {
    return router.push('/scripts/sistema')
  }

  function goPacotes(sistema: string) {
    return router.push({ path: '/scripts/pacotes', query: { sistema } })
  }

  function goLista(sistema: string, pacote: number | string, extra?: Record<string, string>) {
    return router.push({
      path: '/scripts',
      query: { sistema, pacote: String(pacote), ...extra }
    })
  }

  return { SISTEMAS, isSistemaValido, goSistema, goPacotes, goLista }
}
