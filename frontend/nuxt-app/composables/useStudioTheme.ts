export type StudioThemeMode = 'dark' | 'light'

const STORAGE_KEY = 'medware-theme'

function readStoredTheme(): StudioThemeMode {
  if (!import.meta.client) return 'dark'
  const stored = localStorage.getItem(STORAGE_KEY)
  return stored === 'light' ? 'light' : 'dark'
}

function applyToShell(mode: StudioThemeMode) {
  if (!import.meta.client) return
  const shell = document.querySelector('.studio-shell')
  if (shell) {
    shell.setAttribute('data-theme', mode)
  }
  document.documentElement.setAttribute('data-theme', mode)
  localStorage.setItem(STORAGE_KEY, mode)
}

export function useStudioTheme() {
  const theme = useState<StudioThemeMode>('studio-medware-theme', () => 'dark')

  const isDark = computed(() => theme.value === 'dark')

  const setTheme = (mode: StudioThemeMode) => {
    theme.value = mode
    applyToShell(mode)
  }

  const toggleTheme = () => {
    setTheme(theme.value === 'dark' ? 'light' : 'dark')
  }

  const initTheme = () => {
    setTheme(readStoredTheme())
  }

  return { theme, isDark, setTheme, toggleTheme, initTheme }
}
