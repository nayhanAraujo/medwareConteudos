import { defineStore } from 'pinia'
import type { CamposScriptRoot, VoiceSessionMode, VoiceSessionResponse, VoiceTranscriptEntry } from '~/types/voice'

export const useVoiceSessionStore = defineStore('voiceSession', {
  state: () => ({
    sessionId: null as string | null,
    isImported: false,
    revision: 0,
    documentHash: null as string | null,
    dependencies: [] as import('~/utils/studioDependencies').DependencyNode[],
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
      const sameSession=this.sessionId===response.id
      this.sessionId = response.id
      const rawMode = String(response.mode).toLowerCase()
      this.mode = rawMode.includes('json') ? 'fromJson' : rawMode.includes('image') ? 'fromImage' : 'fromScratch'
      this.isImported = response.isImported === true || this.mode === 'fromJson'
      this.revision = response.revision ?? 0
      this.documentHash=response.documentHash??null
      this.dependencies=response.dependencies??[]
      this.camposScriptJson = response.camposScriptJson
      this.transcriptHistory = sameSession&&!response.transcriptHistory?.length ? this.transcriptHistory : response.transcriptHistory ?? []
      this.sourceFileName = response.sourceFileName ?? null
    },

    updateCamposScript(json: string, documentHash?: string | null) {
      this.camposScriptJson = json
      if(documentHash)this.documentHash=documentHash
    },

    addHistory(entry: VoiceTranscriptEntry) {
      this.transcriptHistory.unshift(entry)
    },

    reset() {
      this.sessionId = null
      this.isImported = false
      this.revision = 0
      this.documentHash=null;this.dependencies=[]
      this.mode = 'fromScratch'
      this.camposScriptJson = '{"camposScript":[]}'
      this.transcriptHistory = []
      this.sourceFileName = null
    }
  }
})
