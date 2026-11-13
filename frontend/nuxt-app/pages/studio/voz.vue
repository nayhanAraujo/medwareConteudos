<script setup lang="ts">
import type { ConversionFormat, ConversionRecord } from '~/types/conversion'
import type { VoiceUtteranceIntent } from '~/types/voice'
import { resolveConversionContent } from '~/utils/conversionFormat'
import type { VoiceSessionResponse } from '~/types/voice'
import type {RemovalBlock} from '~/utils/studioDependencies'

definePageMeta({ layout: 'studio', studioAction: 'voz' })
const integrated = ref(false)
const integratedAvailable = ref(false)
const capabilities=ref({guidedVoice:false,persistentDrafts:false,realtimeVoice:false,conventionalVoice:true})
const conventionalOptions=ref(false)
const voiceBlock=ref<RemovalBlock|null>(null)
const temporaryView=ref<{locate:(block:RemovalBlock)=>void}|null>(null)
onMounted(async () => { try { capabilities.value=await useApi().get<typeof capabilities.value>('/api/voice/capabilities');integratedAvailable.value=capabilities.value.persistentDrafts } catch {try{integratedAvailable.value=(await useApi().get<{enabled:boolean}>('/api/voice/rascunhos/status')).enabled}catch{}} })

const voiceStore = useVoiceSessionStore()
const conversionStore = useConversionStore()
const { createSession, applyUtterance, generateLaudo, transcribeAudio } = useVoiceApi()
const { toast, chooseConversionFormat } = useStudioSwal()
const { downloadHtml, downloadText, downloadJson, copyToClipboard } = useHtmlPreview()
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
const realtimeEnabled = ref(false)
const realtimeActive = ref(false)
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

const onJsonImported = (result: {session?: VoiceSessionResponse}) => {
  if (!result.session) return
  voiceStore.applySession(result.session)
  outputFormat.value = null; generatedHtml.value = ''; generatedText.value = ''; validation.value = null
  resetSpeech()
  toast('Modelo JSON importado. As configurações originais foram preservadas.', 'success')
}

const applyTranscript = async (intent: VoiceUtteranceIntent) => {
  if (sending.value || generating.value || !voiceStore.sessionId || !editableTranscript.value.trim()) return false

  const text = editableTranscript.value.trim()
  const sessionId = voiceStore.sessionId
  const previousModel = voiceStore.camposScriptJson
  const effectiveIntent: VoiceUtteranceIntent = hasModel.value ? 'edit' : intent
  if (isListening.value) stopSpeech()

  sending.value = true
  startProgress()
  try {
    const result = await applyUtterance(sessionId, text, effectiveIntent, 'browser')
    if (voiceStore.sessionId !== sessionId) return false
    if (result.revision != null && result.revision < voiceStore.revision) return false
    const model = JSON.parse(result.camposScriptJson)
    if (!Array.isArray(model?.camposScript) || (hasModel.value && model.camposScript.length === 0)) {
      throw new Error('O processamento retornou um modelo inválido ou vazio. O laudo anterior foi preservado.')
    }
    voiceStore.updateCamposScript(result.camposScriptJson,result.documentHash)
    if (result.revision != null) voiceStore.revision = result.revision
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

    if (voiceStore.isImported) {
      generatedHtml.value = ''; generatedText.value = ''; outputFormat.value = null; validation.value = null
      toast(result.summary, isWarning ? 'warning' : 'success')
      if (result.warnings.length) toast(result.warnings.join(' '), 'warning')
      transcript.value = ''; speechError.value = null
      return true
    }

    if (hasFields && !isWarning) {
      try {
        await exportModoTextoAuto()
      } catch (e: unknown) {
        transcript.value = ''
        speechError.value = null
        const detail = e instanceof Error ? ` ${e.message}` : ''
        toast(`O modelo foi atualizado, mas não foi possível gerar o TXT. Seu laudo e o preview anterior foram preservados; tente gerar novamente.${detail}`, 'warning')
        return true
      }
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
    return hasFields && !isWarning
  } catch (e: unknown) {
    if (voiceStore.sessionId === sessionId) voiceStore.updateCamposScript(previousModel)
    if((e as {code?:string})?.code==='measure_in_use'){voiceBlock.value=(e as {details:RemovalBlock}).details;return false}
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

  try {
    const result = await generateLaudo(voiceStore.sessionId, 'modoTexto')
    const resolved = resolveConversionContent('modoTexto', result)
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
  } finally {
    generating.value = false
    stopProgress()
  }
}

const onStopSpeech = async () => {
  stopSpeech()
  await nextTick()
  if (editableTranscript.value.trim()) {
    await applyTranscript(hasModel.value ? 'edit' : 'build')
  }
}

const onBuildModel = () => applyTranscript(hasModel.value ? 'edit' : 'build')
const onEditModel = () => {
  if (!hasModel.value) {
    toast('Gere o modelo primeiro falando e parando a gravação', 'warning')
    return
  }
  applyTranscript('edit')
}

const onUploadAudio = async (file: File) => {
  if (sending.value || generating.value) return
  if (!voiceStore.sessionId) {
    toast('Inicie uma sessão primeiro', 'warning')
    return
  }
  sending.value = true
  try {
    const text = await transcribeAudio(file, file.name)
    editableTranscript.value = text
    toast(voiceStore.isImported ? 'Áudio transcrito — revise e aplique a edição à estrutura.' : 'Áudio transcrito — clique em Gerar modelo e TXT ou pare após revisar', 'success')
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Falha na transcrição'
    toast(msg, 'error')
  } finally {
    sending.value = false
  }
}

const onGenerate = async () => {
  if (sending.value || generating.value || !voiceStore.sessionId) return
  const format = voiceStore.isImported ? 'jsonStudio' : await chooseConversionFormat()
  if (!format) return

  generating.value = true
  startProgress()

  try {
    const result = await generateLaudo(voiceStore.sessionId, format)
    const resolved = resolveConversionContent(format, result)
    if (resolved.apiMismatch || (format === 'jsonStudio' && !resolved.text)) {
      throw new Error('A API não retornou o formato solicitado. Verifique se a versão atualizada da API foi publicada.')
    }
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
    return true
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Falha ao gerar laudo'
    toast(msg, 'error')
    return false
  } finally {
    generating.value = false
    stopProgress()
  }
}

const onOpenEditor = () => {
  router.push('/studio/editor')
}
async function guidedDownload(){if(!await onGenerate())return;if(outputFormat.value==='jsonStudio')downloadJson(generatedText.value,'modelo.json');else if(outputFormat.value==='modoTexto')downloadText(generatedText.value,'modelo.txt');else if(outputFormat.value==='html')downloadHtml(generatedHtml.value,'modelo.html')}
function locateVoiceDependents(block:RemovalBlock){if(temporaryView.value){temporaryView.value.locate(block);return}const target=block.dependents[0]?.measure;if(!target)return;nextTick(()=>[...document.querySelectorAll<HTMLElement>('[data-source-id]')].find(e=>e.dataset.sourceId===(target.sourceId??target.itemId))?.scrollIntoView({behavior:'smooth',block:'center'}))}

const onNewSession = () => {
  if (sending.value || generating.value || realtimeActive.value) return
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
    <StudioDependencyNotice :block="voiceBlock" @close="voiceBlock=null" @locate="locateVoiceDependents" />
    <div v-if="capabilities.guidedVoice&&!conventionalOptions" class="mx-auto max-w-[1680px] px-4 py-6 sm:px-6">
      <p class="mb-4 text-sm text-ds-text-secondary">Abra um modelo, fale a alteração e confira o resultado antes de salvar ou baixar.</p>
      <StudioIntegratedEditor v-if="capabilities.persistentDrafts" guided :realtime-available="capabilities.realtimeVoice" />
      <StudioTemporaryVoice v-else ref="temporaryView" :realtime-available="capabilities.realtimeVoice" :busy="showProgress" :listening="isListening" :speech-supported="isSupported" :speech-error="speechError" v-model:transcript="editableTranscript" @started="session=>onJsonImported({session})" @speak="startSpeech" @stop="stopSpeech" @apply="onBuildModel" @download="guidedDownload" @blocked="voiceBlock=$event" />
      <details class="mt-6 rounded border border-ds-divider p-4 text-sm"><summary>Outras opções de edição</summary><button class="mt-3 rounded border border-ds-divider p-2" @click="conventionalOptions=true">Abrir fluxo convencional</button></details>
    </div>
    <template v-else>
    <button v-if="capabilities.guidedVoice" class="m-4 rounded border border-ds-divider p-2" :disabled="realtimeActive||isListening||showProgress" @click="conventionalOptions=false">Voltar à edição guiada</button>
    <label v-if="integratedAvailable" class="mb-4 flex items-center gap-2 text-sm"><input v-model="integrated" type="checkbox" :disabled="realtimeActive"> Usar editor integrado com rascunhos e versões</label>
    <StudioIntegratedEditor v-if="integrated" />
    <template v-else>
    <StudioDsPageHeader
      title="Modo voz"
      :subtitle="voiceStore.isImported ? 'Edite a estrutura por voz e baixe o JSON completo, preservando as configurações originais.' : 'Descreva o laudo falando — ao parar, o agente Cursor monta o modelo e gera o TXT automaticamente.'"
      icon="bi-mic"
    />

    <StudioDsPageShell>
      <StudioJsonImport destination="conventional" :disabled="sending || generating || realtimeActive || isListening" @confirmed="onJsonImported" />
      <p v-if="voiceStore.isImported" class="mb-4 text-sm text-amber-500">Modelo importado: fórmulas e configurações preservadas. Edite seções e medidas por voz e baixe o JSON completo. Esta sessão expira e não sobrevive ao reinício da API.</p>
      <div v-if="!sessionActive" class="max-w-2xl">
        <StudioVoiceSessionSetup ref="setupRef" @start="onStartSession" />
      </div>

      <template v-else>
        <div class="mb-4 flex items-center justify-between gap-3">
          <p class="text-sm text-ds-muted">
            Sessão <span class="font-mono text-ds-text-secondary">{{ voiceStore.sessionId }}</span>
            · modo {{ voiceStore.mode === 'fromImage' ? 'com imagem' : 'do zero' }}
          </p>
          <StudioDsButton variant="ghost" icon="bi-arrow-counterclockwise" :disabled="showProgress || realtimeActive" @click="onNewSession">
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
            <label class="flex items-center gap-2 text-sm text-ds-text-secondary">
              <input v-model="realtimeEnabled" type="checkbox" :disabled="showProgress || realtimeActive">
              Usar agente de voz em tempo real (xAI)
            </label>
            <StudioRealtimeVoicePanel v-if="realtimeEnabled" @active="realtimeActive = $event" @blocked="voiceBlock=$event" />
            <StudioVoiceRecorder
              :imported="voiceStore.isImported"
              v-else
              :is-supported="isSupported"
              :is-listening="isListening"
              :has-model="hasModel"
              v-model:transcript="editableTranscript"
              :error="speechError"
              :sending="showProgress"
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
              :imported="voiceStore.isImported"
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

            <StudioDsCard v-if="outputFormat === 'jsonStudio' && generatedText" title="JSON Studio">
              <pre class="max-h-96 overflow-auto rounded-ds-btn bg-ds-surface p-3 text-xs text-ds-text-secondary">{{ generatedText }}</pre>
              <div class="mt-3 flex flex-wrap gap-2">
                <StudioDsButton variant="secondary" icon="bi-download" @click="downloadJson(generatedText, 'laudo-voz.json')">
                  Baixar JSON Studio
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
    </template>
    </template>
  </div>
</template>
