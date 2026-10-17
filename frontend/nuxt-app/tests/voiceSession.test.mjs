import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { computed, ref } from 'vue'

const source = await readFile(new URL('../pages/studio/voz.vue', import.meta.url), 'utf8')
const script = source.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1]
const code = stripTypeScriptTypes(script.replace(/^import .*$/gm, ''))
const initialModel = JSON.stringify({ camposScript: [{ nome: 'ALTURA' }, { nome: 'PESO' }] })

function harness({ response, exportFails = false, applyFails = false } = {}) {
  const calls = []
  const messages = []
  const store = {
    sessionId: 'session', camposScriptJson: initialModel, transcriptHistory: [],
    get camposScript() { return JSON.parse(this.camposScriptJson) },
    updateCamposScript(json) { this.camposScriptJson = json },
    addHistory(entry) { this.transcriptHistory.push(entry) }
  }
  const dependencies = {
    computed, ref, definePageMeta() {}, onUnmounted() {}, nextTick: async () => {},
    useVoiceSessionStore: () => store,
    useConversionStore: () => ({ setCurrent() {} }),
    useVoiceApi: () => ({
      async applyUtterance(...args) {
        calls.push(args)
        if (applyFails) throw new Error('Falha no agente')
        return response ?? { camposScriptJson: initialModel, summary: 'Alterações aplicadas.', warnings: [] }
      },
      async generateLaudo() {
        if (exportFails) throw new Error('Falha na exportação')
        return { validation: { isValid: true }, convertedAt: '', provider: 'test' }
      }
    }),
    useStudioSwal: () => ({ toast: (...args) => messages.push(args) }),
    useHtmlPreview: () => ({}), useRouter: () => ({}),
    useSpeechRecognition: () => ({ isListening: ref(false), transcript: ref('troque a ordem do peso e altura'), error: ref(null), stop() {} }),
    resolveConversionContent: () => ({ format: 'modoTexto', text: 'TXT atualizado', html: '' })
  }
  const setup = new Function(...Object.keys(dependencies), `${code}\nreturn { applyTranscript, generatedText, onStopSpeech };`)
  const page = setup(...Object.values(dependencies))
  page.generatedText.value = 'TXT anterior'
  return { ...page, store, calls, messages }
}

test('existing voice model sends edit even when build was requested', async () => {
  const page = harness()
  assert.equal(await page.applyTranscript('build'), true)
  assert.equal(page.calls[0][2], 'edit')
  assert.equal(page.generatedText.value, 'TXT atualizado')
})

test('stopping a recording edits the current model', async () => {
  const page = harness()
  await page.onStopSpeech()
  assert.equal(page.calls[0][2], 'edit')
  assert.equal(page.store.camposScriptJson, initialModel)
})

test('empty or malformed response preserves the model and previous TXT', async () => {
  for (const json of ['{"camposScript":[]}', '{}', 'invalid']) {
    const page = harness({ response: { camposScriptJson: json, summary: 'OK', warnings: [] } })
    assert.equal(await page.applyTranscript('edit'), false)
    assert.equal(page.store.camposScriptJson, initialModel)
    assert.equal(page.generatedText.value, 'TXT anterior')
    assert.equal(page.store.transcriptHistory.length, 0)
  }
})

test('agent failure preserves model and preview', async () => {
  const page = harness({ applyFails: true })
  assert.equal(await page.applyTranscript('edit'), false)
  assert.equal(page.store.camposScriptJson, initialModel)
  assert.equal(page.generatedText.value, 'TXT anterior')
})

test('TXT failure keeps the successfully edited model and previous preview', async () => {
  const updated = JSON.stringify({ camposScript: [{ nome: 'PESO' }, { nome: 'ALTURA' }] })
  const page = harness({ exportFails: true, response: { camposScriptJson: updated, summary: 'Ordem alterada.', warnings: [] } })
  assert.equal(await page.applyTranscript('edit'), true)
  assert.equal(page.store.camposScriptJson, updated)
  assert.equal(page.generatedText.value, 'TXT anterior')
  assert.match(page.messages.at(-1)[0], /modelo foi atualizado/)
})

test('a second submit is ignored while a voice operation is in progress', async () => {
  const page = harness()
  const first = page.applyTranscript('edit')
  assert.equal(await page.applyTranscript('edit'), false)
  await first
  assert.equal(page.calls.length, 1)
})
