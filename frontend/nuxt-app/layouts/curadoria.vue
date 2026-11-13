<template>
  <div class="curadoria-shell">
    <a class="curadoria-skip" href="#conteudo">Ir para o conteúdo</a>
    <header class="curadoria-header">
      <div class="curadoria-container">
        <p class="curadoria-brand">Medware</p>
        <p class="curadoria-app">Curadoria de Conhecimento</p>
      </div>
    </header>
    <main id="conteudo" class="curadoria-container curadoria-main" tabindex="-1">
      <slot />
    </main>
    <footer class="curadoria-footer">
      <nav class="curadoria-container curadoria-links" aria-label="Documentos do aplicativo">
        <NuxtLink to="/curadoria/privacidade">Privacidade</NuxtLink>
        <NuxtLink to="/curadoria/termos">Termos de uso</NuxtLink>
      </nav>
    </footer>
  </div>
</template>

<script setup lang="ts">
useHead({ htmlAttrs: { lang: 'pt-BR' } })
</script>

<style>
.curadoria-shell { min-height: 100dvh; display: flex; flex-direction: column; background: #f8fafc; color: #172033; font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; line-height: 1.75; }
.curadoria-shell * { box-sizing: border-box; }
.curadoria-container { width: min(100%, 56rem); margin-inline: auto; padding-inline: clamp(1.25rem, 5vw, 3rem); }
.curadoria-header { border-bottom: 1px solid #dbe3ed; background: #fff; padding-block: 1.5rem; }
.curadoria-brand { margin: 0; color: #1d4ed8; font-size: .85rem; font-weight: 750; letter-spacing: .12em; text-transform: uppercase; }
.curadoria-app { margin: .2rem 0 0; font-size: 1rem; font-weight: 600; }
.curadoria-main { flex: 1; padding-block: clamp(2rem, 5vw, 3.5rem); }
.curadoria-main:focus { outline: none; }
.curadoria-footer { background: #fff; border-top: 1px solid #dbe3ed; padding-block: 1.5rem; }
.curadoria-links { display: flex; flex-wrap: wrap; gap: .75rem 2rem; font-size: .95rem; }
.curadoria-shell a { color: #1d4ed8; text-decoration: underline; text-underline-offset: .2em; }
.curadoria-shell a:hover { color: #173a8a; }
.curadoria-shell a:focus-visible { outline: 3px solid #1d4ed8; outline-offset: 4px; border-radius: 2px; }
.curadoria-links a[aria-current="page"] { font-weight: 700; }
.curadoria-skip { position: fixed; top: .5rem; left: 1rem; z-index: 100; padding: .5rem 1rem; background: #fff; transform: translateY(-200%); }
.curadoria-skip:focus { transform: translateY(0); }
@media print { .curadoria-skip, .curadoria-footer { display: none; } .curadoria-shell { background: #fff; } }
</style>
