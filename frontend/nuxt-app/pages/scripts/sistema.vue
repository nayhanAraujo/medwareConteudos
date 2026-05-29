<template>
  <div>
    <LayoutAppPageHeader title="Seleção de Sistema" icon="gear-fill" />
    <div class="card shadow-sm">
      <div class="card-body text-center">
        <h5 class="fw-bold mb-4">Escolha o Sistema</h5>
        <p class="text-muted mb-4">Selecione o sistema para visualizar os scripts disponíveis</p>
        <div class="row justify-content-center g-4">
          <div class="col-md-5">
            <div class="system-card card h-100 p-4" role="button" @click="select('Laudos UX')">
              <div class="card-body">
                <i class="bi bi-pc-display-horizontal fs-1 text-primary" />
                <h5 class="mt-3">Laudos UX</h5>
                <p class="text-muted small mb-0">Sistema moderno de laudos</p>
              </div>
            </div>
          </div>
          <div class="col-md-5">
            <div class="system-card card h-100 p-4" role="button" @click="select('Laudos Flex')">
              <div class="card-body">
                <i class="bi bi-laptop fs-1 text-info" />
                <h5 class="mt-3">Laudos Flex</h5>
                <p class="text-muted small mb-0">Sistema tradicional de laudos</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
const { goPacotes } = useScriptsNav()
function select(sistema: string) {
  goPacotes(sistema)
}
</script>
