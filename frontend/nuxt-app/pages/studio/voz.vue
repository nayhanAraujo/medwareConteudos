<script setup lang="ts">
import type { ConversionFormat, ConversionRecord } from '~/types/conversion'
import type { VoiceUtteranceIntent } from '~/types/voice'
import { resolveConversionContent } from '~/utils/conversionFormat'

definePageMeta({ layout: 'studio', studioAction: 'voz' })

const voiceStore = useVoiceSessionStore()
const conversionStore = useConversionStore()
const { createSession, applyUtterance, generateLaudo, transcribeAudio } = useVoiceApi()
const { toast, chooseConversionFormat } = useStudioSwal()
const { downloadHtml, downloadText, copyToClipboard } = useHtmlPreview()
const router = useRouter()

const setupRef = ref<{ setLoading: (v: boolean) => void } | null>(null)
const {
  isSupported,
  isListening,
  transcript,
  error: speechError,
  start: startSpeech,
  stop: stopSpeech,
  reset: resetSpeech
} = useSpeechRecognition()

const sessionActive = computed(() => !!voiceStore.sessionId)
const hasModel = computed(() => voiceStore.camposScript.camposScript.length > 0)
const sending = ref(false)
const generating = ref(false)
const elapsedMs = ref(0)
const EXPECTED_SECONDS = 45
const progressKind = computed<'voice-build' | 'voice-export'>(() =>
  sending.value && !generating.value ? 'voice-build' : 'voice-export'
)
const showProgress = computed(() => sending.value || generating.value)
const progressPercent = computed(() => {
  if (!showProgress.value) return 0
  const seconds = elapsedMs.value / 1000
  return Math.min(92, 6 + (1 - Math.exp(-seconds / EXPECTED_SECONDS)) * 86)
})

let progressTimer: ReturnType<typeof setInterval> | null = null
let progressStartedAt = 0

const startProgress = () => {
  progressStartedAt = Date.now()
  elapsedMs.value = 0
  if (progressTimer) clearInterval(progressTimer)
  progressTimer = setInterval(() => {
    elapsedMs.value = Date.now() - progressStartedAt
  }, 200)
}

const stopProgress = () => {
  if (progressTimer) {
    clearInterval(progressTimer)
    progressTimer = null
  }
}

onUnmounted(stopProgress)
const outputFormat = ref<ConversionFormat | null>(null)
const generatedHtml = ref('')
const generatedText = ref('')
const validation = ref<ConversionRecord['validation'] | null>(null)

const editableTranscript = computed({
  get: () => transcript.value,
  set: (v: string) => { transcript.value = v }
})

const onStartSession = async (mode: 'fromScratch' | 'fromImage', image?: File) => {
  try {
    const session = await createSession(mode, image)
    voiceStore.applySession({
      ...session,
      id: String(session.id)
    })
    toast('Sessão iniciada', 'success')
    if (mode === 'fromImage') {
      toast('Modelo inicial carregado — fale para ajustar ou use Editar modelo', 'info')
    }
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Falha ao criar sessão'
    toast(msg, 'error')
  } finally {
    setupRef.value?.setLoading(false)
  }
}

const applyTranscript = async (intent: VoiceUtteranceIntent) => {
  if (!voiceStore.sessionId || !editableTranscript.value.trim()) return false

  const text = editableTranscript.value.trim()
  if (isListening.value) stopSpeech()

  sending.value = true
  startProgress()
  try {
    const result = await applyUtterance(voiceStore.sessionId, text, intent, 'browser')
    voiceStore.updateCamposScript(result.camposScriptJson)
    voiceStore.addHistory({
      at: new Date().toISOString(),
      transcript: text,
      sttSource: 'browser',
      summary: result.summary
    })

    const hasFields = voiceStore.camposScript.camposScript.length > 0
    const isWarning = result.warnings.length > 0
      || result.summary.toLowerCase().includes('não foi possível')
      || result.summary.toLowerCase().includes('nenhuma alteração')

    if (intent === 'build' && hasFields && !isWarning) {
      await exportModoTextoAuto()
      toast(`${result.summary} — TXT modo texto gerado.`, 'success')
      transcript.value = ''
      speechError.value = null
      return true
    }

    toast(result.summary, isWarning ? 'warning' : 'success')

    if (!isWarning) {
      transcript.value = ''
      speechError.value = null
    }
    return hasFields
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Falha ao processar fala'
    toast(msg, 'error')
    return false
  } finally {
    sending.value = false
    if (!generating.value) stopProgress()
  }
}

const exportModoTextoAuto = async () => {
  if (!voiceStore.sessionId) return

  generating.value = true
  outputFormat.value = 'modoTexto'
  generatedHtml.value = ''
  generatedText.value = ''
  validation.value = null

  try {
    const result = await generateLaudo(voiceStore.sessionId, 'modoTexto')
    const resolved = resolveConversionContent('modoTexto', result)
    outputFormat.value = resolved.format
    generatedText.value = resolved.text
    validation.value = result.validation

    const record: ConversionRecord = {
      id: crypto.randomUUID(),
      format: resolved.format,
      html: resolved.html,
      text: resolved.text,
      sourceFileName: result.sourceFileName || 'laudo-voz',
      convertedAt: result.convertedAt,
      provider: result.provider,
      validation: result.validation
    }
    conversionStore.setCurrent(record)
  } finally {
    generating.value = false
    stopProgress()
  }
}

const onStopSpeech = async () => {
  stopSpeech()
  await nextTick()
  if (editableTranscript.value.trim()) {
    await applyTranscript('build')
  }
}

const onBuildModel = () => applyTranscript('build')
const onEditModel = () => {
  if (!hasModel.value) {
    toast('Gere o modelo primeiro falando e parando a gravação', 'warning')
    return
  }
  applyTranscript('edit')
}

const onUploadAudio = async (file: File) => {
  if (!voiceStore.sessionId) {
    toast('Inicie uma sessão primeiro', 'warning')
    return
  }
  sending.value = true
  try {
    const text = await transcribeAudio(file, file.name)
    editableTranscript.value = text
    toast('Áudio transcrito — clique em Gerar modelo e TXT ou pare após revisar', 'success')
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Falha na transcrição'
    toast(msg, 'error')
  } finally {
    sending.value = false
  }
}

const onGenerate = async () => {
  if (!voiceStore.sessionId) return
  const format = await chooseConversionFormat()
  if (!format) return

  generating.value = true
  startProgress()
  outputFormat.value = format
  generatedHtml.value = ''
  generatedText.value = ''
  validation.value = null

  try {
    const result = await generateLaudo(voiceStore.sessionId, format)
    const resolved = resolveConversionContent(format, result)
    outputFormat.value = resolved.format
    generatedHtml.value = resolved.html
    generatedText.value = resolved.text
    validation.value = result.validation

    const record: ConversionRecord = {
      id: crypto.randomUUID(),
      format: resolved.format,
      html: resolved.html,
      text: resolved.text,
      sourceFileName: result.sourceFileName || 'laudo-voz',
      convertedAt: result.convertedAt,
      provider: result.provider,
      validation: result.validation
    }
    conversionStore.setCurrent(record)
    toast('Laudo gerado', result.validation.isValid ? 'success' : 'warning')
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Falha ao gerar laudo'
    toast(msg, 'error')
  } finally {
    generating.value = false
    stopProgress()
  }
}

const onOpenEditor = () => {
  router.push('/studio/editor')
}

const onNewSession = () => {
  stopProgress()
  voiceStore.reset()
  resetSpeech()
  outputFormat.value = null
  generatedHtml.value = ''
  generatedText.value = ''
  validation.value = null
}
</script>

<template>
  <div>
    <StudioDsPageHeader
      title="Modo voz"
      subtitle="Descreva o laudo falando — ao parar, o agente Cursor monta o modelo e gera o TXT automaticamente."
      icon="bi-mic"
    />

    <StudioDsPageShell>
      <div v-if="!sessionActive" class="max-w-2xl">
        <StudioVoiceSessionSetup ref="setupRef" @start="onStartSession" />
      </div>

      <template v-else>
        <div class="mb-4 flex items-center justify-between gap-3">
          <p class="text-sm text-ds-muted">
            Sessão <span class="font-mono text-ds-text-secondary">{{ voiceStore.sessionId }}</span>
            · modo {{ voiceStore.mode === 'fromImage' ? 'com imagem' : 'do zero' }}
          </p>
          <StudioDsButton variant="ghost" icon="bi-arrow-counterclockwise" @click="onNewSession">
            Nova sessão
          </StudioDsButton>
        </div>

        <StudioConversionProgress
          v-if="showProgress"
          class="mb-6"
          :elapsed-ms="elapsedMs"
          :percent="progressPercent"
          :kind="progressKind"
          format="modoTexto"
        />

        <div class="grid gap-6 lg:grid-cols-2">
          <div class="space-y-6">
            <StudioVoiceRecorder
              :is-supported="isSupported"
              :is-listening="isListening"
              :has-model="hasModel"
              v-model:transcript="editableTranscript"
              :error="speechError"
              :sending="sending"
              @start="startSpeech"
              @stop="onStopSpeech"
              @build="onBuildModel"
              @edit="onEditModel"
              @upload-audio="onUploadAudio"
            />
            <StudioVoiceTranscriptPanel :history="voiceStore.transcriptHistory" />
          </div>

          <div class="space-y-6">
            <StudioVoiceModelPreview :campos="voiceStore.camposScript.camposScript" />
            <StudioVoiceGeneratePanel
              :loading="generating"
              :has-session="sessionActive"
              :output-format="outputFormat"
              :validation="validation"
              @generate="onGenerate"
              @open-editor="onOpenEditor"
            />

            <StudioDsCard v-if="outputFormat === 'html' && generatedHtml" title="Preview HTML">
              <StudioHtmlPreviewFrame :html="generatedHtml" />
              <div class="mt-3 flex flex-wrap gap-2">
                <StudioDsButton variant="secondary" icon="bi-download" @click="downloadHtml(generatedHtml, 'laudo-voz.html')">
                  Baixar HTML
                </StudioDsButton>
                <StudioDsButton variant="ghost" icon="bi-clipboard" @click="copyToClipboard(generatedHtml)">
                  Copiar
                </StudioDsButton>
              </div>
            </StudioDsCard>

            <StudioDsCard v-if="outputFormat === 'modoTexto' && generatedText" title="Preview TXT">
              <StudioModoTextoPreview :text="generatedText" />
              <div class="mt-3 flex flex-wrap gap-2">
                <StudioDsButton variant="secondary" icon="bi-download" @click="downloadText(generatedText, 'laudo-voz.txt')">
                  Baixar TXT
                </StudioDsButton>
                <StudioDsButton variant="ghost" icon="bi-clipboard" @click="copyToClipboard(generatedText)">
                  Copiar
                </StudioDsButton>
              </div>
            </StudioDsCard>
          </div>
        </div>
      </template>
    </StudioDsPageShell>
  </div>
</template>
