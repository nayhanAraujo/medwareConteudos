import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { computed, ref } from 'vue'

const source = await readFile(new URL('../components/studio/VoiceWorkspaceHeader.vue', import.meta.url), 'utf8')
const script = stripTypeScriptTypes(source.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1])

function workspace(download) {
  let exposed
  const setup = new Function('ref', 'computed', 'defineProps', 'defineEmits', 'defineExpose',
    `${script}\nreturn { startDownload, current, downloaded };`)
  const state = setup(ref, computed, () => ({ phase: 'review', download }), () => {}, value => { exposed = value })
  return { ...state, ...exposed }
}

test('cancelled download returns to review and does not mark completion', async () => {
  const state = workspace(async () => false)
  await state.startDownload()
  assert.equal(state.current.value, 2)
  assert.equal(state.downloaded.value, false)
})

test('download completes only after the handoff, ignores duplicate clicks, and resets on edit', async () => {
  let handoff
  let calls = 0
  const state = workspace(() => { calls++; return new Promise(resolve => { handoff = resolve }) })
  const first = state.startDownload()
  assert.equal(state.current.value, 3)
  assert.equal(state.downloaded.value, false)
  await state.startDownload()
  assert.equal(calls, 1)
  handoff(true)
  await first
  assert.equal(state.downloaded.value, true)
  state.reset()
  assert.equal(state.downloaded.value, false)
})
