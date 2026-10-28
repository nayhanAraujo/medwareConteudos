import type { Ref } from 'vue'
import type { ScriptListItem } from '~/composables/useScriptsApi'

export function useGlobalScriptSearch(sistema: Ref<string>) {
  const api = useScriptsApi()
  const query = ref('')
  const includeInactive = ref(false)
  const items = ref<ScriptListItem[]>([])
  const loading = ref(false)
  const error = ref('')
  const page = ref(1)
  const totalPages = ref(0)
  const totalItems = ref(0)
  const ready = computed(() => query.value.trim().length >= 3)
  let timer: ReturnType<typeof setTimeout> | undefined
  let revision = 0

  async function search(targetPage = 1) {
    clearTimeout(timer)
    const current = ++revision
    if (!ready.value || !['Laudos UX', 'Laudos Flex'].includes(sistema.value)) { loading.value = false; return }
    loading.value = true
    error.value = ''
    items.value = []
    page.value = targetPage
    try {
      const result = await api.listScripts({
        nome: query.value.trim(), sistema: sistema.value,
        ativo: includeInactive.value ? undefined : 1,
        page: targetPage, pageSize: 10
      })
      if (current !== revision) return
      items.value = result.data
      totalPages.value = result.totalPages
      totalItems.value = result.totalItems
    } catch (cause) {
      if (current !== revision) return
      error.value = cause instanceof Error ? cause.message : 'Erro ao buscar modelos.'
      totalPages.value = 0
      totalItems.value = 0
    } finally {
      if (current === revision) loading.value = false
    }
  }

  watch([query, includeInactive, sistema], () => {
    ++revision
    clearTimeout(timer)
    items.value = []
    error.value = ''
    page.value = 1
    totalPages.value = 0
    totalItems.value = 0
    loading.value = ready.value
    if (ready.value) timer = setTimeout(() => search(), 350)
  }, { flush: 'sync' })

  onBeforeUnmount(() => { clearTimeout(timer); ++revision })
  return { query, includeInactive, items, loading, error, page, totalPages, totalItems, ready, search }
}
