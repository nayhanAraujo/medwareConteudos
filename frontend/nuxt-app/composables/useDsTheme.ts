export type DsThemeName = 'blue' | 'green' | 'orange' | 'gray' | 'purple' | 'rose' | 'slate' | 'dark'

export interface DsCardTheme {
  gradient: string
  border: string
  iconBg: string
  iconColor: string
  badgeBg?: string
  dark?: boolean
}

const themes: Record<DsThemeName, DsCardTheme> = {
  blue: {
    gradient: 'bg-gradient-to-br from-blue-50 to-white',
    border: 'border-blue-100',
    iconBg: 'bg-blue-100',
    iconColor: 'text-blue-600',
    badgeBg: 'bg-blue-600 text-white'
  },
  green: {
    gradient: 'bg-gradient-to-br from-green-50 to-white',
    border: 'border-green-100',
    iconBg: 'bg-green-100',
    iconColor: 'text-green-600',
    badgeBg: 'bg-green-600 text-white'
  },
  orange: {
    gradient: 'bg-gradient-to-br from-orange-50 to-white',
    border: 'border-orange-100',
    iconBg: 'bg-orange-100',
    iconColor: 'text-orange-600',
    badgeBg: 'bg-orange-600 text-white'
  },
  gray: {
    gradient: 'bg-gradient-to-br from-gray-50 to-white',
    border: 'border-gray-200',
    iconBg: 'bg-gray-100',
    iconColor: 'text-gray-600',
    badgeBg: 'bg-gray-600 text-white'
  },
  purple: {
    gradient: 'bg-gradient-to-br from-purple-50 to-white',
    border: 'border-purple-100',
    iconBg: 'bg-purple-100',
    iconColor: 'text-purple-600',
    badgeBg: 'bg-purple-600 text-white'
  },
  rose: {
    gradient: 'bg-gradient-to-br from-rose-50 to-white',
    border: 'border-rose-100',
    iconBg: 'bg-rose-100',
    iconColor: 'text-rose-600',
    badgeBg: 'bg-rose-600 text-white'
  },
  slate: {
    gradient: 'bg-gradient-to-br from-slate-100 to-white',
    border: 'border-slate-200',
    iconBg: 'bg-slate-200',
    iconColor: 'text-slate-700',
    badgeBg: 'bg-slate-700 text-white'
  },
  dark: {
    gradient: 'bg-gradient-to-br from-black to-gray-700',
    border: 'border-gray-800',
    iconBg: 'bg-white/20',
    iconColor: 'text-white',
    badgeBg: 'bg-green-500 text-white',
    dark: true
  }
}

const delayClasses = ['animate-delay-200', 'animate-delay-400', 'animate-delay-600', 'animate-delay-800']

export function useDsTheme() {
  function getTheme(name: DsThemeName): DsCardTheme {
    return themes[name]
  }

  function delayClass(index: number): string {
    return delayClasses[index % delayClasses.length]
  }

  return { themes, getTheme, delayClass }
}
