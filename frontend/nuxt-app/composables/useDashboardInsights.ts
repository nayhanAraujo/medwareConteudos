export type DashboardPeriod = 'hoje' | 'ontem' | '7d' | '30d'

export interface DashboardAccent {
  name: string
  border: string
  glow: string
  iconBg: string
  iconColor: string
  spark: string
}

export interface DashboardMetricDef {
  key: keyof DashboardStats
  label: string
  icon: string
  accent: DashboardAccent
}

export interface DashboardStats {
  variaveis: number
  referencias: number
  formulas: number
  scripts: number
  relatorios: number
  paineis: number
  modelosMensagens: number
  impressos: number
  usuarios: number
}

export interface DashboardActivityItem {
  user: string
  action: string
  entity: string
  time: string
  message: string
  icon: string
}

export interface ReferenciasPorAnoSeries {
  labels: string[]
  totals: number[]
}

export interface DashboardReferenciasAnoItem {
  year: string
  total: number
}

const PERIOD_POINTS: Record<DashboardPeriod, number> = {
  hoje: 8,
  ontem: 8,
  '7d': 7,
  '30d': 30
}

export const DASHBOARD_METRICS: DashboardMetricDef[] = [
  { key: 'variaveis', label: 'Variáveis', icon: 'braces-asterisk', accent: accent('blue', '#2563eb') },
  { key: 'referencias', label: 'Referências', icon: 'journal-medical', accent: accent('cyan', '#0891b2') },
  { key: 'formulas', label: 'Fórmulas', icon: 'calculator-fill', accent: accent('green', '#16a34a') },
  { key: 'scripts', label: 'Scripts', icon: 'code-square', accent: accent('indigo', '#4f46e5') },
  { key: 'relatorios', label: 'Relatórios', icon: 'file-earmark-bar-graph', accent: accent('amber', '#d97706') },
  { key: 'paineis', label: 'Painéis', icon: 'bar-chart-fill', accent: accent('purple', '#7c3aed') },
  { key: 'modelosMensagens', label: 'Modelos de mensagens', icon: 'chat-square-heart', accent: accent('rose', '#e11d48') },
  { key: 'impressos', label: 'Impressos', icon: 'printer-fill', accent: accent('slate', '#475569') },
  { key: 'usuarios', label: 'Usuários', icon: 'people-fill', accent: accent('teal', '#0d9488') }
]

function accent(name: string, spark: string): DashboardAccent {
  const palette: Record<string, Omit<DashboardAccent, 'name' | 'spark'>> = {
    blue: { border: 'border-blue-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(37,99,235,0.35)]', iconBg: 'bg-blue-500/15', iconColor: 'text-blue-600' },
    cyan: { border: 'border-cyan-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(8,145,178,0.32)]', iconBg: 'bg-cyan-500/15', iconColor: 'text-cyan-600' },
    green: { border: 'border-emerald-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(22,163,74,0.32)]', iconBg: 'bg-emerald-500/15', iconColor: 'text-emerald-600' },
    indigo: { border: 'border-indigo-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(79,70,229,0.35)]', iconBg: 'bg-indigo-500/15', iconColor: 'text-indigo-600' },
    amber: { border: 'border-amber-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(217,119,6,0.32)]', iconBg: 'bg-amber-500/15', iconColor: 'text-amber-600' },
    purple: { border: 'border-violet-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(124,58,237,0.35)]', iconBg: 'bg-violet-500/15', iconColor: 'text-violet-600' },
    rose: { border: 'border-rose-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(225,29,72,0.32)]', iconBg: 'bg-rose-500/15', iconColor: 'text-rose-600' },
    slate: { border: 'border-slate-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(71,85,105,0.28)]', iconBg: 'bg-slate-500/15', iconColor: 'text-slate-600' },
    teal: { border: 'border-teal-400/45', glow: 'shadow-[0_10px_28px_-12px_rgba(13,148,136,0.32)]', iconBg: 'bg-teal-500/15', iconColor: 'text-teal-600' }
  }
  return { name, spark, ...palette[name] }
}

function hashString(value: string): number {
  let hash = 2166136261
  for (let i = 0; i < value.length; i++) {
    hash ^= value.charCodeAt(i)
    hash = Math.imul(hash, 16777619)
  }
  return hash >>> 0
}

function seededRandom(seed: number) {
  return () => {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0
    return seed / 0xffffffff
  }
}

function average(values: number[]): number {
  if (!values.length) return 0
  return values.reduce((sum, value) => sum + value, 0) / values.length
}

export function buildMetricSeries(label: string, total: number, period: DashboardPeriod): number[] {
  const rng = seededRandom(hashString(label))
  const points = 30
  const base = Math.max(total, 1)
  const series: number[] = []
  let value = base * (0.82 + rng() * 0.12)
  for (let i = 0; i < points; i++) {
    value = Math.max(0, value + (rng() - 0.47) * base * 0.045)
    series.push(Math.round(value))
  }
  series[series.length - 1] = total

  const windowSize = PERIOD_POINTS[period]
  if (period === 'ontem') return series.slice(-(windowSize * 2), -windowSize)
  return series.slice(-windowSize)
}

export function computeVariation(series: number[]): number {
  if (series.length < 2) return 0
  const mid = Math.max(1, Math.floor(series.length / 2))
  const previous = average(series.slice(0, mid))
  const current = average(series.slice(mid))
  if (previous === 0) return current > 0 ? 100 : 0
  return Math.round(((current - previous) / previous) * 100)
}

export function toReferenciasPorAnoSeries(items: DashboardReferenciasAnoItem[]): ReferenciasPorAnoSeries {
  return {
    labels: items.map(item => item.year),
    totals: items.map(item => item.total)
  }
}

export function buildFallbackActivity(userName: string): DashboardActivityItem[] {
  const user = userName || 'clinicas'
  const samples = [
    { action: 'criou novo Script', entity: 'Hemograma Completo', time: '20:52:48', icon: 'code-square' },
    { action: 'atualizou a Fórmula', entity: 'Clearance de Creatinina', time: '19:41:12', icon: 'calculator-fill' },
    { action: 'publicou o Painel', entity: 'Indicadores Assistenciais', time: '18:15:03', icon: 'bar-chart-fill' },
    { action: 'cadastrou a Variável', entity: 'Glicemia Jejum', time: '17:08:36', icon: 'braces-asterisk' },
    { action: 'revisou a Referência', entity: 'Consenso Lipidêmico 2024', time: '16:22:09', icon: 'journal-medical' },
    { action: 'gerou o Relatório', entity: 'Produção Mensal', time: '15:04:51', icon: 'file-earmark-bar-graph' }
  ]
  return samples.map(item => ({
    user,
    ...item,
    message: `Utilizador ${user} ${item.action} '${item.entity}' — ${item.time}`
  }))
}

export function useDashboardInsights() {
  return {
    metrics: DASHBOARD_METRICS,
    buildMetricSeries,
    computeVariation,
    toReferenciasPorAnoSeries,
    buildFallbackActivity
  }
}
