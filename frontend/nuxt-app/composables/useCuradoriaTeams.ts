import { createCuradoriaTeamsController, initialCuradoriaTeamsView, type CuradoriaTeamsKind, type CuradoriaTeamsTheme } from '~/utils/curadoriaTeams'
import { verifyCuradoriaPilotChat } from '~/utils/curadoriaPilot'

export function useCuradoriaTeams(kind: CuradoriaTeamsKind) {
  const view = reactive(initialCuradoriaTeamsView())
  const hostTheme = useState<CuradoriaTeamsTheme>('curadoria-teams-theme', () => 'default')
  let controller: ReturnType<typeof createCuradoriaTeamsController> | undefined

  onMounted(() => {
    controller = createCuradoriaTeamsController(kind, () => import('@microsoft/teams-js'), next => {
      Object.assign(view, next)
      hostTheme.value = next.theme
    }, 8000, verifyCuradoriaPilotChat)
    void controller.start()
  })
  onBeforeUnmount(() => { controller?.dispose(); hostTheme.value = 'default' })
  return { view, setConfirmed: (confirmed: boolean) => controller?.setConfirmed(confirmed) }
}
