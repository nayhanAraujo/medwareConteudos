<template>
  <DsAuthShell background-image="/images/auth/login-bg.webp">
    <h2 class="text-xl font-semibold font-manrope text-ds-text mb-6">Recuperar senha</h2>
    <p class="text-sm text-ds-text-secondary">
      Para recuperar o acesso, entre em contato com o administrador do sistema.
      A redefinição de senha deve ser realizada pelo administrador após confirmar sua identidade.
    </p>
    <p class="text-center mt-4 mb-0 text-sm">
      <NuxtLink to="/login" class="text-blue-600 hover:underline">Voltar ao login</NuxtLink>
    </p>
  </DsAuthShell>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })
</script>
