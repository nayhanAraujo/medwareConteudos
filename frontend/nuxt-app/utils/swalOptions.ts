import type { SweetAlertOptions } from 'sweetalert2'

export type NotificationIcon = 'success' | 'error' | 'warning' | 'info'

export function notificationOptions(title: string, icon: NotificationIcon, text?: string): SweetAlertOptions {
  return {
    toast: true,
    position: 'top-end',
    icon,
    titleText: title,
    text,
    width: 'min(23rem, calc(100vw - 2rem))',
    padding: '0.875rem 1rem',
    showConfirmButton: false,
    showCloseButton: true,
    closeButtonAriaLabel: 'Fechar notificação',
    timer: icon === 'error' ? undefined : 3000,
    timerProgressBar: false,
    customClass: {
      popup: 'mdw-notification',
      title: 'mdw-notification-title',
      htmlContainer: 'mdw-notification-text',
      closeButton: 'mdw-notification-close'
    },
    didOpen: (popup) => {
      // Keep messages readable while the user hovers or operates the close button.
      void import('sweetalert2').then(({ default: Swal }) => {
        popup.addEventListener('mouseenter', () => Swal.stopTimer())
        popup.addEventListener('mouseleave', () => Swal.resumeTimer())
        popup.addEventListener('focusin', () => Swal.stopTimer())
        popup.addEventListener('focusout', () => Swal.resumeTimer())
      })
    }
  }
}

export const compactDialogOptions: SweetAlertOptions = {
  width: 'min(25rem, calc(100vw - 2rem))',
  padding: '1.5rem',
  backdrop: 'rgba(15, 23, 42, 0.22)',
  buttonsStyling: false,
  reverseButtons: true,
  focusCancel: true,
  customClass: {
    popup: 'mdw-dialog',
    title: 'mdw-dialog-title',
    htmlContainer: 'mdw-dialog-text',
    actions: 'mdw-dialog-actions',
    confirmButton: 'mdw-dialog-confirm',
    cancelButton: 'mdw-dialog-cancel',
    denyButton: 'mdw-dialog-deny'
  }
}
