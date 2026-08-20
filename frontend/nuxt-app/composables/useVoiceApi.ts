import type { ConversionFormat, ConversionResponse } from '~/types/conversion'
import type {
  VoiceSessionMode,
  VoiceSessionResponse,
  VoiceSttSource,
  VoiceUtteranceIntent,
  VoiceUtteranceResponse
} from '~/types/voice'

export function useVoiceApi() {
  const config = useRuntimeConfig()
  const apiBase = config.public.apiBase as string

  const createSession = async (mode: VoiceSessionMode, image?: File): Promise<VoiceSessionResponse> => {
    const formData = new FormData()
    formData.append('mode', mode === 'fromImage' ? 'FromImage' : 'FromScratch')
    if (image) formData.append('image', image)

    return await $fetch<VoiceSessionResponse>(`${apiBase}/api/voice/sessions`, {
      method: 'POST',
      body: formData
    })
  }

  const getSession = async (sessionId: string): Promise<VoiceSessionResponse> =>
    await $fetch<VoiceSessionResponse>(`${apiBase}/api/voice/sessions/${sessionId}`)

  const applyUtterance = async (
    sessionId: string,
    transcript: string,
    intent: VoiceUtteranceIntent = 'build',
    sttSource: VoiceSttSource = 'browser'
  ): Promise<VoiceUtteranceResponse> =>
    await $fetch<VoiceUtteranceResponse>(`${apiBase}/api/voice/sessions/${sessionId}/utterance`, {
      method: 'POST',
      body: {
        transcript,
        intent: intent === 'edit' ? 'edit' : 'build',
        sttSource: sttSource === 'server' ? 'Server' : 'Browser'
      }
    })

  const generateLaudo = async (sessionId: string, format: ConversionFormat): Promise<ConversionResponse> =>
    await $fetch<ConversionResponse>(`${apiBase}/api/voice/sessions/${sessionId}/generate`, {
      method: 'POST',
      body: { format }
    })

  const transcribeAudio = async (audio: Blob, fileName = 'gravacao.webm'): Promise<string> => {
    const formData = new FormData()
    formData.append('audio', audio, fileName)
    const result = await $fetch<{ transcript: string }>(`${apiBase}/api/voice/transcribe`, {
      method: 'POST',
      body: formData
    })
    return result.transcript
  }

  return { createSession, getSession, applyUtterance, generateLaudo, transcribeAudio }
}
