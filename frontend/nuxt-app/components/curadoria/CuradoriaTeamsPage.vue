<template>
  <article class="curadoria-teams-page" aria-labelledby="teams-title">
    <p class="curadoria-pilot">Piloto interno · Em preparação</p>
    <h1 id="teams-title">{{ configuration ? 'Configurar a aba no Teams' : 'Medware — Curadoria de Conhecimento' }}</h1>
    <p class="curadoria-intro">Informações técnicas reutilizáveis, organizadas como sugestões para revisão humana.</p>

    <div class="curadoria-status" role="status" aria-live="polite" aria-atomic="true">{{ view.message }}</div>

    <details v-if="configuration && view.diagnostic" class="curadoria-diagnostic" :open="view.mode === 'unsupported'">
      <summary>Diagnóstico da configuração (sem dados pessoais)</summary>
      <dl>
        <div><dt>Host</dt><dd>{{ view.diagnostic.host }}</dd></div>
        <div><dt>Frame</dt><dd>{{ view.diagnostic.frame }}</dd></div>
        <div><dt>Configuração suportada</dt><dd>{{ yesNo(view.diagnostic.configurationSupported) }}</dd></div>
        <div><dt>Identificador de chat presente</dt><dd>{{ yesNo(view.diagnostic.chatIdPresent) }}</dd></div>
        <div><dt>Identificador de equipe presente</dt><dd>{{ yesNo(view.diagnostic.teamIdPresent) }}</dd></div>
        <div><dt>Identificador de canal presente</dt><dd>{{ yesNo(view.diagnostic.channelIdPresent) }}</dd></div>
        <div><dt>Identificador de reunião presente</dt><dd>{{ yesNo(view.diagnostic.meetingIdPresent) }}</dd></div>
      </dl>
      <p>O Teams pode não informar o chat antes de salvar a aba. Este diagnóstico não identifica o grupo e não comprova autorização de acesso.</p>
    </details>

    <section v-if="configuration" aria-labelledby="configuration-title">
      <h2 id="configuration-title">Adicionar somente a aba informativa</h2>
      <p>O salvamento deve ocorrer dentro do Microsoft Teams, ao adicionar esta aba ao chat em grupo <strong>Desenvolvimento | Demandas</strong>. No navegador comum, esta página apresenta orientações, mas não instala o aplicativo.</p>
      <label class="curadoria-confirmation">
        <input type="checkbox" :checked="view.confirmed" :disabled="!view.canConfigure || view.mode === 'saving' || view.mode === 'saved'" @change="setConfirmed(($event.target as HTMLInputElement).checked)" />
        <span>Confirmo que estou adicionando a aba ao chat em grupo autorizado para o piloto. Entendo que salvar a aba não ativa a leitura de mensagens.</span>
      </label>
      <p class="curadoria-help">Após confirmar, use o botão <strong>Salvar</strong> da janela do Teams. Esta página não solicita senha nem credenciais.</p>
      <a href="https://conteudo.medware.com.br/curadoria/teams/aba" target="_blank" rel="noopener noreferrer">Conhecer a aba informativa (nova janela)</a>
    </section>

    <section aria-labelledby="purpose-title">
      <h2 id="purpose-title">Finalidade e escopo inicial</h2>
      <p>O aplicativo de uso interno foi definido para apoiar a seleção de conhecimento técnico no chat em grupo Desenvolvimento | Demandas. O Curador de Conhecimento poderá preparar sugestões de procedimentos, dúvidas frequentes e soluções técnicas para avaliação humana.</p>
    </section>
    <section aria-labelledby="access-title">
      <h2 id="access-title">Acesso e coleta são etapas separadas</h2>
      <p>O acesso depende de instalação e consentimento na organização, das permissões aplicáveis e de um coletor separado devidamente configurado. Salvar ou visualizar esta aba não comprova acesso ao Microsoft Graph. A coleta e o acesso RSC ainda precisam ser validados no ambiente Teams.</p>
      <p><strong>Estas páginas não leem mensagens, não enviam nem alteram conteúdos no Teams e não gravam dados por ODBC.</strong> Não há publicação automática de artigos neste piloto.</p>
    </section>
    <section aria-labelledby="review-title">
      <h2 id="review-title">Revisão humana obrigatória</h2>
      <p>As sugestões podem conter erros ou interpretações incorretas. Uma pessoa deve conferir as fontes, o contexto e a solução antes de qualquer uso ou publicação. Senhas, tokens, dados clínicos e informações pessoais ou de clientes desnecessárias devem ser removidos ou anonimizados.</p>
    </section>
    <section aria-labelledby="contact-title">
      <h2 id="contact-title">Contato responsável</h2>
      <p>Dúvidas e solicitações sobre o aplicativo: <a href="mailto:nayhan@medware.com.br">nayhan@medware.com.br</a>.</p>
      <p>Consulte também os documentos de privacidade e termos de uso nos links abaixo.</p>
    </section>
  </article>
</template>

<script setup lang="ts">
const props = defineProps<{ configuration?: boolean }>()
const { view, setConfirmed } = useCuradoriaTeams(props.configuration ? 'configuration' : 'content')
const yesNo = (value: boolean) => value ? 'Sim' : 'Não'
</script>

<style scoped>
.curadoria-teams-page { overflow-wrap: anywhere; }
.curadoria-pilot { margin: 0 0 .75rem; color: var(--curadoria-muted); font-size: .85rem; font-weight: 600; }
h1 { margin: 0; max-width: 26ch; color: var(--curadoria-text); font-size: clamp(1.7rem, 4vw, 2.25rem); font-weight: 700; line-height: 1.3; letter-spacing: -.02em; }
.curadoria-intro { margin: 1rem 0 1.5rem; color: var(--curadoria-muted); }
.curadoria-status { padding: 1rem 1.25rem; border: 1px solid var(--curadoria-border); border-left: 4px solid var(--curadoria-link); background: var(--curadoria-surface); border-radius: .5rem; margin-bottom: 2rem; }
section + section { margin-top: 1.75rem; }
h2 { margin: 0 0 .65rem; color: var(--curadoria-text); font-size: 1.15rem; font-weight: 650; line-height: 1.45; }
p { margin: 0 0 .8rem; }
.curadoria-confirmation { display: flex; gap: .8rem; align-items: flex-start; padding: 1rem; border: 1px solid var(--curadoria-border); border-radius: .5rem; margin-block: 1.25rem; background: var(--curadoria-surface); cursor: pointer; }
input { width: 1.15rem; height: 1.15rem; flex: 0 0 auto; margin-top: .3rem; accent-color: var(--curadoria-link); }
input:focus-visible { outline: 3px solid var(--curadoria-link); outline-offset: 4px; }
.curadoria-help { font-size: .95rem; color: var(--curadoria-muted); }
.curadoria-diagnostic { margin: -1rem 0 1.5rem; font-size: .85rem; color: var(--curadoria-muted); }
summary { cursor: pointer; }
summary:focus-visible { outline: 3px solid var(--curadoria-link); outline-offset: 4px; }
dl { margin: .75rem 0; }
dl > div { display: flex; flex-wrap: wrap; gap: .3rem .75rem; }
dt { font-weight: 600; }
dd { margin: 0; }
</style>
