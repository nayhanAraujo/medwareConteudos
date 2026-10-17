import { compactDialogOptions, notificationOptions, type NotificationIcon } from '~/utils/swalOptions'

export function useSwal() {
  const toast = async (title: string, icon: NotificationIcon = 'success', text?: string) => {
    if (!import.meta.client) return
    const { default: Swal } = await import('sweetalert2')
    // Display immediately; callers can refresh or navigate without waiting for dismissal.
    void Swal.fire(notificationOptions(title, icon, text))
  }

  const confirm = async (title: string, text?: string) => {
    if (!import.meta.client) return { isConfirmed: false }
    const { default: Swal } = await import('sweetalert2')
    return Swal.fire({
      ...compactDialogOptions,
      titleText: title,
      text,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: /exclu/i.test(title) ? 'Excluir' : 'Confirmar',
      cancelButtonText: 'Cancelar'
    })
  }

  const error = async (title: string, text?: string) => {
    return toast(title, 'error', text)
  }

  const info = async (title: string, text?: string) => {
    return toast(title, 'info', text)
  }

  const designSystemRuleAcceptance = async (title: string, text: string) => {
    if (!import.meta.client) return { isConfirmed: false, isDenied: false }
    const { default: Swal } = await import('sweetalert2')
    return Swal.fire({
      ...compactDialogOptions,
      focusCancel: false,
      titleText: title,
      text,
      icon: 'info',
      confirmButtonText: 'Entendi',
      showDenyButton: true,
      denyButtonText: 'Vou fazer do meu jeito',
      allowOutsideClick: false,
      allowEscapeKey: false
    })
  }

  const warning = async (title: string, text?: string) => {
    return toast(title, 'warning', text)
  }

  return { toast, confirm, error, info, warning, designSystemRuleAcceptance }
}
