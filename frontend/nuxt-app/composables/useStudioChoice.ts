import type { Ref } from 'vue'
import type { StudioDraft, StudioVariable } from '~/types/studioDraft'

interface Prompt { draftId?: string; revision: number; candidates: StudioVariable[]; sectionId?: string; token: string }
interface Request { token: string; codVariavel: number; sectionId: string; commandId: string }
export function useStudioChoice(options: {draft: Ref<StudioDraft | null>; selectedSection: Ref<string | null>; api: Pick<ReturnType<typeof useApi>, 'post' | 'get'>; accept: (draft: StudioDraft) => void}) {
  const choices = ref<Prompt | null>(null)
  const choiceSection = ref<string | null>(null)
  const choiceBusy = ref(false)
  const choiceError = ref('')
  const request = ref<Request | null>(null)
  const choiceCode = computed(() => request.value?.codVariavel ?? null)
  let draftId: string | null = null
  const choiceStale = computed(() => !!choices.value && (draftId !== options.draft.value?.id || choices.value.revision !== options.draft.value?.revision))
  watch(() => options.draft.value?.id, () => { choices.value = null; request.value = null; choiceError.value = '' }, {flush: 'sync'})
  function receiveChoice(value: Prompt) {
    const current = options.draft.value
    if (!current || (value.draftId && value.draftId !== current.id) || choices.value) return
    draftId = current.id; choices.value = value; request.value = null; choiceError.value = ''
    const section = value.sectionId || options.selectedSection.value
    choiceSection.value = current.document.secoes.some(s => s.id === section) ? section! : null
  }
  async function resolveChoice(code: number) {
    const current = choices.value; const id = draftId
    if (!current || !id || choiceBusy.value || options.draft.value?.id !== id) return
    if (request.value && request.value.codVariavel !== code) return
    if (!request.value && (choiceStale.value || !choiceSection.value || !options.draft.value.document.secoes.some(s => s.id === choiceSection.value))) return
    request.value ??= {token: current.token, codVariavel: code, sectionId: choiceSection.value!, commandId: crypto.randomUUID()}
    const operation = request.value
    choiceBusy.value = true; choiceError.value = ''
    try {
      const value = await options.api.post<StudioDraft>(`/api/voice/rascunhos/${id}/escolhas`, operation)
      if (options.draft.value?.id === id && choices.value === current) {
        options.accept(value); options.selectedSection.value = operation.sectionId
        choices.value = null; request.value = null
      }
    } catch (e) {
      if (choices.value === current) choiceError.value = `${e instanceof Error ? e.message : 'Não foi possível aplicar a escolha.'} A janela foi mantida; uma nova tentativa reutiliza a mesma operação.`
    } finally { choiceBusy.value = false }
  }
  async function cancelChoice() {
    const current = choices.value; const id = draftId
    if (!current || !id || choiceBusy.value) return
    choiceBusy.value = true; choiceError.value = ''
    try {
      // A lost reply may already have committed; check the confirmed state before closing.
      if (request.value) {
        const latest = await options.api.get<StudioDraft>(`/api/voice/rascunhos/${id}`)
        if (options.draft.value?.id === id) options.accept(latest)
      }
      await options.api.post(`/api/voice/rascunhos/${id}/escolhas/cancelar`, {token: current.token})
      if (choices.value === current) { choices.value = null; request.value = null }
    } catch (e) { if (choices.value === current) choiceError.value = e instanceof Error ? e.message : 'Não foi possível cancelar as sugestões.' }
    finally { choiceBusy.value = false }
  }
  return {choices, choiceSection, choiceBusy, choiceError, choiceStale, choiceCode, receiveChoice, resolveChoice, cancelChoice}
}
