export type MeasureMove = { sourceId: string; sectionId: string; beforeItemId: string | null }
export type GesturePoint = { x: number; y: number }
type Landmark = GesturePoint & { z: number }
export type GestureHand = Landmark[]

export const trackingGapMs = 250
const holdMs = 100
const closeRatio = 0.28
const openRatio = 0.42

export function pinchRatio(hand: GestureHand, aspect: number) {
  const distance = (a: Landmark, b: Landmark) =>
    Math.hypot((a.x - b.x) * aspect, a.y - b.y, (a.z - b.z) * aspect)
  const wrist = hand[0], thumb = hand[4], index = hand[8], palm = hand[9]
  if (!wrist || !thumb || !index || !palm) return null
  const palmSize = distance(wrist, palm)
  return palmSize > 0.01 ? distance(thumb, index) / palmSize : null
}

export function createPinch() {
  let state: 'unarmed' | 'open' | 'closed' = 'unarmed'
  let since: number | null = null
  let lastFrame: number | null = null

  function reset() {
    state = 'unarmed'
    since = null
    lastFrame = null
  }

  function sample(ratio: number | null, timestamp: number) {
    if (ratio === null || !Number.isFinite(ratio)) {
      reset()
      return 'lost'
    }
    if (lastFrame !== null && timestamp - lastFrame > trackingGapMs) {
      reset()
      lastFrame = timestamp
      return 'lost'
    }
    lastFrame = timestamp
    const transitioning = state === 'open' ? ratio <= closeRatio : ratio >= openRatio
    if (!transitioning) {
      since = null
      return
    }
    since ??= timestamp
    if (timestamp - since < holdMs) return
    since = null
    if (state === 'unarmed') {
      state = 'open'
      return
    }
    state = state === 'open' ? 'closed' : 'open'
    return state === 'closed' ? 'grab' : 'release'
  }

  return { reset, sample }
}

export function resolveMeasureMove(
  sections: readonly { id: string; items: readonly string[] }[],
  { sourceId, sectionId, beforeItemId }: MeasureMove
) {
  const source = sections.find(section => section.items.includes(sourceId))
  const destination = sections.find(section => section.id === sectionId)
  if (!source || !destination) return null
  const index = beforeItemId === null ? destination.items.length : destination.items.indexOf(beforeItemId)
  if (index < 0) return null
  const previous = source.id === sectionId ? source.items.indexOf(sourceId) : -1
  const position = previous >= 0 && previous < index ? index - 1 : index
  return position === previous ? null : position
}
