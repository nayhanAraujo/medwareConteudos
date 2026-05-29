export function useClock() {
  const now = ref('')

  function format() {
    const d = new Date()
    const date = d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
    const time = d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })
    now.value = `${date} ${time}`
  }

  onMounted(() => {
    format()
    const id = setInterval(format, 1000)
    onUnmounted(() => clearInterval(id))
  })

  return { now }
}
