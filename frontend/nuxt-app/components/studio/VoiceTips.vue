<script setup lang="ts">
function showTips() {
  return useStudioSwal().alert('Como organizar o modelo por voz', [
    'Mover para outra seção: “Mova Peso para a seção Dados do paciente.”',
    'Reordenar: “Na seção Dados do paciente, coloque Altura antes de Peso.”',
    'Mover ao início: “Coloque Superfície Corporal como primeira medida da seção Dados do paciente.”',
    'Mudar de coluna: “Mova a seção Valvas para a coluna direita.”',
    'Mover só uma medida de coluna: “Mova Gradiente aórtico para a seção Valvas da coluna direita.”',
    'Criar destino: “Crie uma seção chamada Valvas na coluna direita.”',
    'Use os nomes que aparecem no modelo. Esquerda é a coluna 1 e direita é a coluna 2. Cada medida pertence a uma seção.',
    'Na gravação por etapas, pare, confira a frase e clique em Aplicar alteração. Na conversação contínua, confira as alterações enquanto fala. Para remover medidas, use os controles do modelo.'
  ].join('\n\n'), 'info')
}
</script>

<template>
  <button type="button" class="my-2 rounded-lg border border-ds-divider px-3 py-2 text-sm focus-visible:outline focus-visible:outline-2"
    @click="showTips"><i class="bi bi-question-circle mr-2" aria-hidden="true" />Dicas de comandos por voz</button>
</template>
