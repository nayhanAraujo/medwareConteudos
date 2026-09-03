export function useHtmlPreview() {
  const buildPreviewDocument = (html: string, executeScripts = false) => {
    const sandbox = executeScripts ? 'allow-scripts allow-same-origin' : 'allow-same-origin'
    return {
      srcdoc: html,
      sandbox
    }
  }

  const downloadHtml = (html: string, fileName = 'laudo-script.html') => {
    const blob = new Blob([html], { type: 'text/html;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    link.click()
    URL.revokeObjectURL(url)
  }

  const downloadText = (text: string, fileName = 'laudo-modo-texto.txt') => {
    const blob = new Blob([text], { type: 'text/plain;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    link.click()
    URL.revokeObjectURL(url)
  }

  const downloadJson = (json: string, fileName = 'laudo-studio.json') => {
    const blob = new Blob([json], { type: 'application/json;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    link.click()
    URL.revokeObjectURL(url)
  }

  const copyToClipboard = async (content: string) => {
    await navigator.clipboard.writeText(content)
  }

  return { buildPreviewDocument, downloadHtml, downloadText, downloadJson, copyToClipboard }
}
