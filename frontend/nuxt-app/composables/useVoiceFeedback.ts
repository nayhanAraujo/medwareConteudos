export function useVoiceFeedback() {
  const highlighted = ref<string[]>([])
  const message = ref('')
  let timer: ReturnType<typeof setTimeout> | undefined
  function clear() { clearTimeout(timer); highlighted.value = []; message.value = '' }
  function show(ids: string[], text: string) {
    clearTimeout(timer)
    highlighted.value = ids
    message.value = text
    timer = setTimeout(() => { highlighted.value = [] }, 6000)
  }
  onBeforeUnmount(() => clearTimeout(timer))
  return { highlighted, message, clear, show }
}
