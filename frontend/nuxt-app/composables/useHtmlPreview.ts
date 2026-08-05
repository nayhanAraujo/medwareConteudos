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

  const copyToClipboard = async (html: string) => {
    await navigator.clipboard.writeText(html)
  }

  return { buildPreviewDocument, downloadHtml, copyToClipboard }
}
