import Swal from 'sweetalert2'
import type { StudioThemeMode } from '~/composables/useStudioTheme'

function themeColors(mode: StudioThemeMode) {
  if (mode === 'light') {
    return {
      background: '#FFFFFF',
      color: '#243447',
      muted: '#5c6b7a',
      optionBg: '#f4f6f8',
      optionBorder: '#d0d7de',
      optionSelectedBg: '#e8f2fc',
      optionSelectedBorder: '#0F6CBD',
      confirmButtonColor: '#0F6CBD'
    }
  }

  return {
    background: '#2B2B2B',
    color: '#F5F5F5',
    muted: '#b0bac5',
    optionBg: '#1f1f1f',
    optionBorder: '#4a4a4a',
    optionSelectedBg: '#1a2a3d',
    optionSelectedBorder: '#0F6CBD',
    confirmButtonColor: '#0F6CBD'
  }
}

function buildFormatPickerHtml(mode: StudioThemeMode) {
  const c = themeColors(mode)

  const optionStyle = (selected: boolean) =>
    [
      'display:flex',
      'align-items:flex-start',
      'gap:12px',
      'padding:14px 16px',
      'margin-bottom:10px',
      'border-radius:10px',
      'cursor:pointer',
      'transition:background 0.15s,border-color 0.15s',
      `border:2px solid ${selected ? c.optionSelectedBorder : c.optionBorder}`,
      `background:${selected ? c.optionSelectedBg : c.optionBg}`,
      `color:${c.color}`
    ].join(';')

  return `
<div id="studio-format-picker" style="margin-top:0.75rem;text-align:left;">
  <label data-format="html" class="studio-format-option" style="${optionStyle(true)}">
    <input type="radio" name="studio-conversion-format" value="html" checked style="margin-top:4px;accent-color:#0F6CBD;width:16px;height:16px;flex-shrink:0;">
    <span style="flex:1;min-width:0;">
      <strong style="display:block;font-size:0.95rem;margin-bottom:4px;color:${c.color};">HTML LaudosUX</strong>
      <span style="display:block;font-size:0.8125rem;line-height:1.4;color:${c.muted};">Preview visual com campos e scripts</span>
    </span>
  </label>
  <label data-format="modoTexto" class="studio-format-option" style="${optionStyle(false)}">
    <input type="radio" name="studio-conversion-format" value="modoTexto" style="margin-top:4px;accent-color:#0F6CBD;width:16px;height:16px;flex-shrink:0;">
    <span style="flex:1;min-width:0;">
      <strong style="display:block;font-size:0.95rem;margin-bottom:4px;color:${c.color};">TXT Modo Texto</strong>
      <span style="display:block;font-size:0.8125rem;line-height:1.4;color:${c.muted};">Arquivo .txt importável no LaudosUX</span>
    </span>
  </label>
  <label data-format="jsonStudio" class="studio-format-option" style="${optionStyle(false)}">
    <input type="radio" name="studio-conversion-format" value="jsonStudio" style="margin-top:4px;accent-color:#0F6CBD;width:16px;height:16px;flex-shrink:0;">
    <span style="flex:1;min-width:0;">
      <strong style="display:block;font-size:0.95rem;margin-bottom:4px;color:${c.color};">JSON Studio</strong>
      <span style="display:block;font-size:0.8125rem;line-height:1.4;color:${c.muted};">Modelo { camposScript } com revisão de variáveis</span>
    </span>
  </label>
</div>`
}

function wireFormatPicker(mode: StudioThemeMode) {
  const c = themeColors(mode)
  const container = document.getElementById('studio-format-picker')
  if (!container) return

  const options = container.querySelectorAll<HTMLLabelElement>('.studio-format-option')

  const applySelected = (selected: HTMLLabelElement) => {
    options.forEach((opt) => {
      const isSelected = opt === selected
      opt.style.borderColor = isSelected ? c.optionSelectedBorder : c.optionBorder
      opt.style.background = isSelected ? c.optionSelectedBg : c.optionBg
      const radio = opt.querySelector('input[type="radio"]') as HTMLInputElement | null
      if (radio) radio.checked = isSelected
    })
  }

  options.forEach((opt) => {
    opt.addEventListener('click', (event) => {
      if ((event.target as HTMLElement).tagName === 'INPUT') return
      applySelected(opt)
    })
    opt.querySelector('input')?.addEventListener('change', () => applySelected(opt))
  })
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

  const chooseConversionFormat = async (): Promise<'html' | 'modoTexto' | 'jsonStudio' | null> => {
    const mode = theme.value
    const colors = themeColors(mode)

    const result = await Swal.fire({
      title: 'Formato de conversão',
      html: `
        <p style="margin:0;font-size:0.9375rem;line-height:1.5;color:${colors.muted};">
          Escolha como deseja converter a imagem:
        </p>
        ${buildFormatPickerHtml(mode)}
      `,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Converter',
      cancelButtonText: 'Cancelar',
      confirmButtonColor: colors.confirmButtonColor,
      cancelButtonColor: mode === 'dark' ? '#555' : '#94a3b8',
      background: colors.background,
      color: colors.color,
      focusConfirm: false,
      customClass: {
        popup: 'studio-swal-popup',
        htmlContainer: 'studio-swal-html'
      },
      didOpen: () => wireFormatPicker(mode),
      preConfirm: () => {
        const selected = document.querySelector<HTMLInputElement>(
          'input[name="studio-conversion-format"]:checked'
        )
        if (!selected?.value) {
          Swal.showValidationMessage('Selecione um formato')
          return false
        }
        return selected.value
      }
    })

    if (!result.isConfirmed || !result.value) return null
    if (result.value === 'modoTexto') return 'modoTexto'
    if (result.value === 'jsonStudio') return 'jsonStudio'
    return 'html'
  }

  return { toast, confirm, alert, chooseConversionFormat }
}
