<template>
  <div class="curadoria-shell" :data-teams-theme="teamsPage ? hostTheme : undefined">
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
        <template v-if="teamsPage">
          <a href="/curadoria/privacidade" target="_blank" rel="noopener noreferrer">Privacidade <span class="curadoria-new-window">(nova janela)</span></a>
          <a href="/curadoria/termos" target="_blank" rel="noopener noreferrer">Termos de uso <span class="curadoria-new-window">(nova janela)</span></a>
        </template>
        <template v-else>
          <NuxtLink to="/curadoria/privacidade">Privacidade</NuxtLink>
          <NuxtLink to="/curadoria/termos">Termos de uso</NuxtLink>
        </template>
      </nav>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { isTeamsCuradoriaPath } from '~/utils/curadoriaPublicRoutes'
import type { CuradoriaTeamsTheme } from '~/utils/curadoriaTeams'
const route = useRoute()
const teamsPage = computed(() => isTeamsCuradoriaPath(route.path))
const hostTheme = useState<CuradoriaTeamsTheme>('curadoria-teams-theme', () => 'default')
useHead({ htmlAttrs: { lang: 'pt-BR' } })
</script>

<style>
.curadoria-shell { --curadoria-bg: #f8fafc; --curadoria-surface: #fff; --curadoria-text: #172033; --curadoria-muted: #475569; --curadoria-border: #dbe3ed; --curadoria-link: #1d4ed8; min-height: 100dvh; display: flex; flex-direction: column; background: var(--curadoria-bg); color: var(--curadoria-text); font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; line-height: 1.75; }
.curadoria-shell[data-teams-theme="dark"] { --curadoria-bg: #202020; --curadoria-surface: #292929; --curadoria-text: #f5f5f5; --curadoria-muted: #d1d5db; --curadoria-border: #5b616b; --curadoria-link: #a9c8ff; color-scheme: dark; }
.curadoria-shell[data-teams-theme="contrast"] { --curadoria-bg: #000; --curadoria-surface: #000; --curadoria-text: #fff; --curadoria-muted: #fff; --curadoria-border: #fff; --curadoria-link: #ffed00; color-scheme: dark; }
.curadoria-shell * { box-sizing: border-box; }
.curadoria-container { width: min(100%, 56rem); margin-inline: auto; padding-inline: clamp(1.25rem, 5vw, 3rem); }
.curadoria-header { border-bottom: 1px solid var(--curadoria-border); background: var(--curadoria-surface); padding-block: 1.5rem; }
.curadoria-brand { margin: 0; color: var(--curadoria-link); font-size: .85rem; font-weight: 750; letter-spacing: .12em; text-transform: uppercase; }
.curadoria-app { margin: .2rem 0 0; font-size: 1rem; font-weight: 600; }
.curadoria-main { flex: 1; padding-block: clamp(2rem, 5vw, 3.5rem); }
.curadoria-main:focus { outline: none; }
.curadoria-footer { background: var(--curadoria-surface); border-top: 1px solid var(--curadoria-border); padding-block: 1.5rem; }
.curadoria-links { display: flex; flex-wrap: wrap; gap: .75rem 2rem; font-size: .95rem; }
.curadoria-shell a { color: var(--curadoria-link); text-decoration: underline; text-underline-offset: .2em; }
.curadoria-shell a:hover { text-decoration-thickness: 2px; }
.curadoria-shell a:focus-visible { outline: 3px solid var(--curadoria-link); outline-offset: 4px; border-radius: 2px; }
.curadoria-links a[aria-current="page"] { font-weight: 700; }
.curadoria-skip { position: fixed; top: .5rem; left: 1rem; z-index: 100; padding: .5rem 1rem; background: var(--curadoria-surface); transform: translateY(-200%); }
.curadoria-new-window { font-size: .85em; }
.curadoria-skip:focus { transform: translateY(0); }
@media print { .curadoria-skip, .curadoria-footer { display: none; } .curadoria-shell { background: #fff; } }
</style>
