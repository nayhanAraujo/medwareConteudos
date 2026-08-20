import { defineStore } from 'pinia'
import type { CamposScriptRoot, VoiceSessionMode, VoiceSessionResponse, VoiceTranscriptEntry } from '~/types/voice'

export const useVoiceSessionStore = defineStore('voiceSession', {
  state: () => ({
    sessionId: null as string | null,
    mode: 'fromScratch' as VoiceSessionMode,
    camposScriptJson: '{"camposScript":[]}' as string,
    transcriptHistory: [] as VoiceTranscriptEntry[],
    sourceFileName: null as string | null
  }),

  getters: {
    camposScript(state): CamposScriptRoot {
      try {
        return JSON.parse(state.camposScriptJson) as CamposScriptRoot
      } catch {
        return { camposScript: [] }
      }
    }
  },

  actions: {
    applySession(response: VoiceSessionResponse) {
      this.sessionId = response.id
      const rawMode = String(response.mode).toLowerCase()
      this.mode = rawMode.includes('image') ? 'fromImage' : 'fromScratch'
      this.camposScriptJson = response.camposScriptJson
      this.transcriptHistory = response.transcriptHistory ?? []
      this.sourceFileName = response.sourceFileName ?? null
    },

    updateCamposScript(json: string) {
      this.camposScriptJson = json
    },

    addHistory(entry: VoiceTranscriptEntry) {
      this.transcriptHistory.unshift(entry)
    },

    reset() {
      this.sessionId = null
      this.mode = 'fromScratch'
      this.camposScriptJson = '{"camposScript":[]}'
      this.transcriptHistory = []
      this.sourceFileName = null
    }
  }
})
