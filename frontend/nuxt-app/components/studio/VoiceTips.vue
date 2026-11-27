<script setup lang="ts">
const dialog = ref<HTMLDialogElement | null>(null)
const examples = [
  { title: 'Mover para outra seção', text: 'Mova Peso para a seção Dados do paciente.' },
  { title: 'Reordenar medidas', text: 'Na seção Dados do paciente, coloque Altura antes de Peso.' },
  { title: 'Mover para o início', text: 'Coloque Superfície Corporal como primeira medida da seção Dados do paciente.' },
  { title: 'Mudar uma seção de coluna', text: 'Mova a seção Valvas para a coluna direita.' },
  { title: 'Mudar uma medida de coluna', text: 'Mova Gradiente aórtico para a seção Valvas da coluna direita.' },
  { title: 'Criar uma seção de destino', text: 'Crie uma seção chamada Valvas na coluna direita.' }
]
onBeforeUnmount(() => dialog.value?.close())
</script>

<template>
  <button type="button" class="my-2 rounded-lg border border-ds-divider px-3 py-2 text-sm focus-visible:outline focus-visible:outline-2"
    @click="dialog?.showModal()"><i class="bi bi-question-circle mr-2" aria-hidden="true" />Dicas de comandos por voz</button>
  <Teleport to="body">
    <dialog ref="dialog" class="voice-tips" aria-labelledby="voice-tips-title" @click=" $event.target === dialog && dialog?.close()">
      <header class="tips-header">
        <div><h2 id="voice-tips-title">Como organizar o modelo por voz</h2><p>Exemplos de comandos</p></div>
        <button type="button" class="tips-close" aria-label="Fechar dicas" autofocus @click="dialog?.close()"><i class="bi bi-x-lg" aria-hidden="true" /></button>
      </header>
      <div class="tips-body">
        <dl class="tips-examples"><div v-for="example in examples" :key="example.title"><dt>{{ example.title }}</dt><dd>“{{ example.text }}”</dd></div></dl>
        <section class="tips-guidance" aria-labelledby="tips-reference"><h3 id="tips-reference">Seções e colunas</h3><p>Use os nomes exibidos no modelo. Esquerda corresponde à coluna 1; direita, à coluna 2. Cada medida pertence a uma seção.</p></section>
        <section class="tips-guidance" aria-labelledby="tips-mode"><h3 id="tips-mode">Ao aplicar uma alteração</h3><ul><li><strong>Por etapas:</strong> pare a gravação, confira a instrução e clique em Aplicar alteração.</li><li><strong>Conversação contínua:</strong> confira as alterações no modelo enquanto fala.</li><li><strong>Remoção:</strong> use os controles do modelo para remover medidas.</li></ul></section>
      </div>
      <footer class="tips-footer"><button type="button" @click="dialog?.close()">Entendi</button></footer>
    </dialog>
  </Teleport>
</template>

<style scoped>
.voice-tips { width: min(800px, calc(100vw - 32px)); max-height: calc(100dvh - 32px); margin: auto; padding: 0; border: 1px solid var(--color-ds-divider, #d0d7de); border-radius: 8px; background: var(--color-ds-surface-elevated, white); color: var(--color-ds-text, #243447); }
.voice-tips::backdrop { background: #0008; }
.voice-tips[open] { display: flex; flex-direction: column; }
.tips-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; padding: 24px; border-bottom: 1px solid var(--color-ds-divider, #d0d7de); }
h2 { margin: 0; font-size: 20px; font-weight: 600; line-height: 1.4; }
.tips-header p { margin: 6px 0 0; font-size: 14px; }
.tips-close { width: 36px; height: 36px; flex: 0 0 36px; border-radius: 4px; }
.tips-body { padding: 0 24px 20px; overflow-y: auto; min-height: 0; }
.tips-examples { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 28px; margin: 0; }
.tips-examples > div { padding: 20px 0; border-bottom: 1px solid var(--color-ds-divider, #d0d7de); }
dt, h3 { font-size: 14px; font-weight: 600; margin: 0 0 8px; }
dd, .tips-guidance { font-size: 14px; line-height: 1.6; overflow-wrap: anywhere; }
dd, .tips-guidance p { margin: 0; }
.tips-guidance { margin-top: 20px; }
.tips-guidance ul { margin: 0; padding-left: 20px; }
.tips-guidance li + li { margin-top: 8px; }
.tips-footer { padding: 16px 24px; border-top: 1px solid var(--color-ds-divider, #d0d7de); display: flex; justify-content: flex-end; }
.tips-footer button { padding: 8px 20px; border: 1px solid var(--color-ds-divider, #d0d7de); border-radius: 4px; }
button:focus-visible { outline: 2px solid var(--color-ds-primary-accent, #0f6cbd); outline-offset: 3px; }
@media (max-width: 600px) { .tips-examples { grid-template-columns: minmax(0, 1fr); } .tips-header { padding: 16px; } .tips-body { padding-inline: 16px; } .tips-footer { padding: 12px 16px; } }
</style>
