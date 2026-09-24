import type { ConversionFormat, ConversionResponse } from '~/types/conversion'
import type {
  VoiceSessionMode,
  VoiceSessionResponse,
  VoiceSttSource,
  VoiceUtteranceIntent,
  VoiceUtteranceResponse
} from '~/types/voice'

export function useVoiceApi() {
  const api = useApi()

  const createSession = async (mode: VoiceSessionMode, image?: File): Promise<VoiceSessionResponse> => {
    const formData = new FormData()
    formData.append('mode', mode === 'fromImage' ? 'FromImage' : 'FromScratch')
    if (image) formData.append('image', image)

    return await api.postForm<VoiceSessionResponse>('/api/voice/sessions', formData)
  }

  const getSession = async (sessionId: string): Promise<VoiceSessionResponse> =>
    await api.get<VoiceSessionResponse>(`/api/voice/sessions/${sessionId}`)

  const applyUtterance = async (
    sessionId: string,
    transcript: string,
    intent: VoiceUtteranceIntent = 'build',
    sttSource: VoiceSttSource = 'browser'
  ): Promise<VoiceUtteranceResponse> =>
    await api.post<VoiceUtteranceResponse>(`/api/voice/sessions/${sessionId}/utterance`, {
      transcript,
      intent: intent === 'edit' ? 'edit' : 'build',
      sttSource: sttSource === 'server' ? 'Server' : 'Browser'
    })

  const generateLaudo = async (sessionId: string, format: ConversionFormat): Promise<ConversionResponse> =>
    await api.post<ConversionResponse>(`/api/voice/sessions/${sessionId}/generate`, { format })

  const transcribeAudio = async (audio: Blob, fileName = 'gravacao.webm'): Promise<string> => {
    const formData = new FormData()
    formData.append('audio', audio, fileName)
    const result = await api.postForm<{ transcript: string }>('/api/voice/transcribe', formData)
    return result.transcript
  }

  return { createSession, getSession, applyUtterance, generateLaudo, transcribeAudio }
}
