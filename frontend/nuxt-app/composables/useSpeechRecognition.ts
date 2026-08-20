export function useSpeechRecognition() {
  const isSupported = ref(false)
  const isListening = ref(false)
  const transcript = ref('')
  const error = ref<string | null>(null)

  let recognition: SpeechRecognition | null = null

  onMounted(() => {
    if (!import.meta.client) return
    const SpeechRecognitionCtor = window.SpeechRecognition || window.webkitSpeechRecognition
    isSupported.value = !!SpeechRecognitionCtor
    if (!SpeechRecognitionCtor) return

    recognition = new SpeechRecognitionCtor()
    recognition.lang = 'pt-BR'
    recognition.continuous = true
    recognition.interimResults = true

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      let combined = ''
      for (let i = 0; i < event.results.length; i++) {
        combined += event.results[i][0].transcript
      }
      transcript.value = combined.trim()
    }

    recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
      // aborted = parada manual; ignored = sessão encerrada sem fala
      if (event.error !== 'aborted' && event.error !== 'no-speech') {
        error.value = event.error
      }
      isListening.value = false
    }

    recognition.onend = () => {
      isListening.value = false
    }
  })

  const start = () => {
    if (!recognition || isListening.value) return
    error.value = null
    isListening.value = true

    try {
      recognition.start()
    } catch {
      // InvalidStateError: sessão anterior ainda encerrando — abort e reinicia
      try {
        recognition.abort()
      } catch { /* ignore */ }
      try {
        recognition.start()
      } catch (e) {
        isListening.value = false
        error.value = e instanceof Error ? e.message : 'Não foi possível iniciar o microfone'
      }
    }
  }

  const stop = () => {
    if (!recognition) return
    isListening.value = false

    try {
      recognition.stop()
    } catch {
      try {
        recognition.abort()
      } catch { /* ignore */ }
    }
  }

  const reset = () => {
    if (isListening.value) stop()
    transcript.value = ''
    error.value = null
  }

  return { isSupported, isListening, transcript, error, start, stop, reset }
}

declare global {
  interface Window {
    SpeechRecognition: typeof SpeechRecognition
    webkitSpeechRecognition: typeof SpeechRecognition
  }
}
