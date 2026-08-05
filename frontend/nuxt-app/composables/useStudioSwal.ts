import Swal from 'sweetalert2'
import type { StudioThemeMode } from '~/composables/useStudioTheme'

function themeColors(mode: StudioThemeMode) {
  if (mode === 'light') {
    return {
      background: '#FFFFFF',
      color: '#243447',
      confirmButtonColor: '#0F6CBD'
    }
  }

  return {
    background: '#2B2B2B',
    color: '#F5F5F5',
    confirmButtonColor: '#0F6CBD'
  }
}

/** Swal com tema do Conversor (Studio). */
export function useStudioSwal() {
  const { theme } = useStudioTheme()

  const toast = (message: string, icon: 'success' | 'error' | 'warning' | 'info' = 'info') => {
    const colors = themeColors(theme.value)
    return Swal.fire({
      toast: true,
      position: 'top-end',
      icon,
      title: message,
      showConfirmButton: false,
      timer: 3000,
      timerProgressBar: true,
      background: colors.background,
      color: colors.color
    })
  }

  const confirm = (title: string, text?: string) => {
    const colors = themeColors(theme.value)
    return Swal.fire({
      title,
      text,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Confirmar',
      cancelButtonText: 'Cancelar',
      confirmButtonColor: colors.confirmButtonColor,
      background: colors.background,
      color: colors.color
    })
  }

  const alert = (title: string, text?: string, icon: 'success' | 'error' | 'warning' | 'info' = 'info') => {
    const colors = themeColors(theme.value)
    return Swal.fire({
      title,
      text,
      icon,
      confirmButtonColor: colors.confirmButtonColor,
      background: colors.background,
      color: colors.color
    })
  }

  return { toast, confirm, alert }
}
