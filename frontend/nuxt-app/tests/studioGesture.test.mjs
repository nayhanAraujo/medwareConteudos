import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { reactive, ref, watch } from 'vue'
import { createPinch, pinchRatio, resolveMeasureMove, trackingGapMs } from '../utils/studioGesture.ts'

test('pinch requires a stable open hand and ignores tremor between thresholds', () => {
  const pinch = createPinch()
  assert.equal(pinch.sample(0.1, 0), undefined)
  assert.equal(pinch.sample(0.1, 120), undefined)
  pinch.sample(0.6, 160)
  pinch.sample(0.6, 260)
  pinch.sample(0.15, 300)
  pinch.sample(0.35, 360)
  pinch.sample(0.15, 400)
  assert.equal(pinch.sample(0.15, 500), 'grab')
  assert.equal(pinch.sample(0.35, 540), undefined)
  pinch.sample(0.6, 580)
  assert.equal(pinch.sample(0.6, 680), 'release')
  assert.equal(pinch.sample(0.6, 780), undefined)
})

test('a missing hand or a stale frame cancels a pinch and disarms release', () => {
  for (const lost of [[null, 250], [0.6, 600]]) {
    const pinch = createPinch()
    pinch.sample(0.6, 0)
    pinch.sample(0.6, 100)
    pinch.sample(0.15, 120)
    assert.equal(pinch.sample(0.15, 220), 'grab')
    assert.equal(pinch.sample(...lost), 'lost')
    assert.equal(pinch.sample(0.6, lost[1] + 100), undefined)
    assert.equal(pinch.sample(0.6, lost[1] + 200), undefined)
  }
})

test('pinch distance is normalized by palm size and camera aspect ratio', () => {
  const hand = new Array(21)
  hand[0] = { x: 0, y: 0, z: 0 }
  hand[9] = { x: 0, y: 0.2, z: 0 }
  hand[4] = { x: 0.1, y: 0.1, z: 0 }
  hand[8] = { x: 0.15, y: 0.1, z: 0 }
  assert.ok(Math.abs(pinchRatio(hand, 2) - 0.5) < 1e-9)
  assert.ok(Math.abs(pinchRatio(hand.map(p => ({ x: p.x / 2, y: p.y / 2, z: p.z })), 2) - 0.5) < 1e-9)
})

const sections = [{ id: 'left', items: ['weight', 'height', 'surface'] }, { id: 'right', items: [] }]
test('resolves downward reorder, cross-column and empty section using the same anchor', () => {
  assert.equal(resolveMeasureMove(sections, { sourceId: 'weight', sectionId: 'left', beforeItemId: 'surface' }), 1)
  assert.equal(resolveMeasureMove(sections, { sourceId: 'surface', sectionId: 'left', beforeItemId: 'weight' }), 0)
  assert.equal(resolveMeasureMove(sections, { sourceId: 'weight', sectionId: 'right', beforeItemId: null }), 0)
  for (const beforeItemId of ['weight', 'height', 'removed']) {
    assert.equal(resolveMeasureMove(sections, { sourceId: 'weight', sectionId: 'left', beforeItemId }), null)
  }
})

const component = await readFile(new URL('../components/studio/GestureControls.vue', import.meta.url), 'utf8')
const script = stripTypeScriptTypes(component.match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1].replace(/^import .*$/gm, ''))
function fixture() {
  const elements = [
    { gestureSection: 'left', gestureItem: 'weight', gestureNext: 'height' },
    { gestureSection: 'left', gestureItem: 'height' },
    { gestureSection: 'right' }
  ].map(dataset => ({
    dataset, closest() { return this },
    getBoundingClientRect: () => ({ top: 200, bottom: 300, height: 100, left: 300, width: 400 })
  }))
  let hit = elements[0]
  let modal = false
  const document = {
    querySelectorAll: () => modal ? [{ getClientRects: () => [1] }] : [],
    elementFromPoint: () => hit
  }
  const props = reactive({
    revision: 1, suspended: '',
    model: { contains: node => elements.includes(node), querySelectorAll: () => elements }
  })
  const events = []
  const setup = new Function(
    'defineProps', 'defineEmits', 'useRuntimeConfig', 'ref', 'watch', 'onMounted', 'onBeforeUnmount',
    'createPinch', 'pinchRatio', 'resolveMeasureMove', 'trackingGapMs', 'window', 'document', 'getComputedStyle',
    script + '\nreturn { updateHand, escape, grabbed, line }'
  )
  const view = setup(
    () => props, () => (type, move) => events.push([type, move]), () => ({ app: { baseURL: '/' } }),
    ref, watch, () => {}, () => {}, createPinch, pinchRatio, resolveMeasureMove, trackingGapMs,
    { innerWidth: 1100, innerHeight: 800 }, document, () => ({ visibility: 'visible' })
  )
  function sample(ratio, time) {
    const hand = []
    hand[0] = { x: 0, y: 0, z: 0 }
    hand[9] = { x: 0, y: 0.2, z: 0 }
    hand[8] = { x: 0.5, y: 0.36, z: 0 }
    hand[4] = { x: 0.5 - ratio * 0.2, y: 0.36, z: 0 }
    view.updateHand(hand, time, 1)
  }
  sample(0.6, 0)
  sample(0.6, 100)
  sample(0.15, 120)
  sample(0.15, 220)
  assert.equal(view.grabbed.value, 'weight')
  return {
    props, view, events,
    release: () => { sample(0.6, 250); sample(0.6, 350); sample(0.6, 450) },
    pointAt: index => { hit = elements[index] },
    openModal: () => { modal = true }
  }
}

test('one confirmed release sends one move after the target row', () => {
  const gesture = fixture()
  gesture.pointAt(1)
  gesture.release()
  assert.deepEqual(gesture.events, [['move', { sourceId: 'weight', sectionId: 'left', beforeItemId: null }]])
  assert.equal(gesture.view.line.value, null)
})

test('releasing over the end of an empty section sends its stable ID', () => {
  const gesture = fixture()
  gesture.pointAt(2)
  gesture.release()
  assert.deepEqual(gesture.events, [['move', { sourceId: 'weight', sectionId: 'right', beforeItemId: null }]])
})

test('release cannot move the measure after cancellation or over an invalid target', () => {
  for (const cancel of [
    gesture => { gesture.props.revision++ },
    gesture => { gesture.props.suspended = 'Voz ativa' },
    gesture => gesture.openModal(),
    gesture => gesture.view.escape({ key: 'Escape' }),
    gesture => gesture.view.updateHand(null, 240, 1),
    gesture => gesture.pointAt(-1),
    gesture => gesture.pointAt(0)
  ]) {
    const gesture = fixture()
    gesture.pointAt(2)
    cancel(gesture)
    gesture.release()
    assert.deepEqual(gesture.events, [])
  }
})
