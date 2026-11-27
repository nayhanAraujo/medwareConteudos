export interface VoiceChangeSection { id: string; name: string; column?: number; items: {id: string; name: string}[] }
export function describeVoiceChange(before: VoiceChangeSection[], after: VoiceChangeSection[]) {
  const oldItems = new Map(before.flatMap(section => section.items.map((item, index) => [item.id, { section, index }] as const)))
  const added = after.flatMap(section => section.items.filter(item => !oldItems.has(item.id)))
  if (added.length) return { ids: added.map(item => item.id), message: added.length === 1 ? `${added[0]!.name} adicionada ao modelo.` : `${added.length} medidas adicionadas ao modelo.` }
  const moved = after.flatMap(section => section.items.flatMap((item, index) => {
    const previous = oldItems.get(item.id)
    return previous && (previous.section.id !== section.id || previous.index !== index)
      ? [{ item, section, index, distance: previous.section.id !== section.id ? Number.MAX_SAFE_INTEGER : Math.abs(previous.index - index) }] : []
  })).sort((a, b) => b.distance - a.distance)
  if (moved.length) {
    const change = moved[0]!
    const previous = change.section.items[change.index - 1]
    return { ids: [change.item.id], message: `${change.item.name} agora está ${previous ? `depois de ${previous.name}` : 'no início'} em ${change.section.name}.` }
  }
  const remaining = new Set(after.flatMap(section => section.items.map(item => item.id)))
  const removed = before.flatMap(section => section.items.filter(item => !remaining.has(item.id)))
  if (removed.length) return { ids: [] as string[], message: removed.length === 1 ? `${removed[0]!.name} removida do modelo.` : `${removed.length} medidas removidas do modelo.` }
  const section = after.find(section => {
    const previous = before.find(candidate => candidate.id === section.id)
    return !previous || previous.name !== section.name || previous.column !== section.column
  })
  if (section) return { ids: [section.id], message: `Seção ${section.name} atualizada.` }
  return { ids: [] as string[], message: 'Modelo atualizado.' }
}
