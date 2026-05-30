export function useSwal() {
  const toast = async (title: string, icon: 'success' | 'error' | 'warning' | 'info' = 'success') => {
    if (!import.meta.client) return
    const { default: Swal } = await import('sweetalert2')
    return Swal.fire({ toast: true, position: 'top-end', icon, title, showConfirmButton: false, timer: 3000 })
  }

  const confirm = async (title: string, text?: string) => {
    if (!import.meta.client) return { isConfirmed: false }
    const { default: Swal } = await import('sweetalert2')
    return Swal.fire({
      title,
      text,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Sim',
      cancelButtonText: 'Cancelar'
    })
  }

  const error = async (title: string, text?: string) => {
    if (!import.meta.client) return
    const { default: Swal } = await import('sweetalert2')
    return Swal.fire({ icon: 'error', title, text, confirmButtonText: 'OK' })
  }

  const info = async (title: string, text?: string) => {
    if (!import.meta.client) return
    const { default: Swal } = await import('sweetalert2')
    return Swal.fire({ icon: 'info', title, text, confirmButtonText: 'Entendi' })
  }

  const designSystemRuleAcceptance = async (title: string, text: string) => {
    if (!import.meta.client) return { isConfirmed: false, isDenied: false }
    const { default: Swal } = await import('sweetalert2')
    return Swal.fire({
      title,
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
    if (!import.meta.client) return
    const { default: Swal } = await import('sweetalert2')
    return Swal.fire({ icon: 'warning', title, text, confirmButtonText: 'OK' })
  }

  return { toast, confirm, error, info, warning, designSystemRuleAcceptance }
}
