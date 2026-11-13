export interface DependencyNode { key: string; itemId: string; codVariavel?: number | null; token?: string | null; nome: string; sectionId?: string | null; sectionName?: string | null; preserved: boolean; dependencies: string[]; sourceId?: string | null }
export interface RemovalBlock {operation: string; removed: DependencyNode[]; dependents: {measure: DependencyNode; requiredKey: string; indirect: boolean}[]}
export function analyzeStudioRemoval(nodes: DependencyNode[], remainingKeys: Set<string>, operation: string): RemovalBlock | null {
  const map = new Map(nodes.map(n => [n.key,n])), removed: DependencyNode[] = [], dependents: RemovalBlock['dependents'] = []
  for (const target of map.values()) {
    if (remainingKeys.has(target.key)) continue
    for (const source of map.values()) {
      if (!remainingKeys.has(source.key)) continue
      const queue = source.dependencies.map(key => ({key,depth:1})), seen = new Set([source.key])
      for (let i=0;i<queue.length;i++) {
        const step=queue[i]!
        if(step.key===target.key){dependents.push({measure:source,requiredKey:target.key,indirect:step.depth>1});break}
        if(seen.has(step.key))continue;seen.add(step.key)
        for(const key of map.get(step.key)?.dependencies || [])queue.push({key,depth:step.depth+1})
      }
    }
    if(dependents.some(d=>d.requiredKey===target.key))removed.push(target)
  }
  return removed.length ? {operation,removed,dependents} : null
}
