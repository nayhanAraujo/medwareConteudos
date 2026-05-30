# Design System — Componentes Nuxt

Biblioteca de componentes Tailwind baseada em [`design-system.html`](../../../static/Desing%20System/hu169yr/design-system.html).

## Tokens (`tailwind.config.ts`)

| Token | Uso |
|-------|-----|
| `bg-ds-page` | Fundo da área de conteúdo (`#cfddea`) |
| `bg-ds-surface` | Superfícies secundárias (`#f1f2f3`) |
| `text-ds-text` | Texto principal |
| `shadow-ds` / `shadow-ds-card` | Sombras glass e cards |
| `font-manrope` / `font-sans` | Tipografia |

## Layout

| Componente | Descrição |
|------------|-----------|
| `DsPageShell` | Painel glass wrapper para páginas autenticadas |
| `DsPageHeader` | Cabeçalho com título, subtítulo, usuário e relógio |
| `DsAuthShell` | Layout centralizado para login/recuperação/aprovação |
| `DsSectionTitle` | Título de seção com slot de ações |

## Navegação e hubs

| Componente | Descrição |
|------------|-----------|
| `DsHubCard` | Card gradiente clicável (seleção de módulos) |
| `DsFeatureCard` | Card com lista de features e CTA |
| `DsModuleCard` | Card de módulo com ações |
| `DsTabs` | Abas estilo DS |
| `DsBreadcrumb` | Trilha de navegação |

## Formulários

| Componente | Descrição |
|------------|-----------|
| `DsInput` | Campo de texto |
| `DsSelect` | Select estilizado |
| `DsTextarea` | Área de texto |
| `DsSearchInput` | Busca com ícone e limpar |
| `DsButton` | Botão primary/secondary/ghost/success/danger |

## Dados e feedback

| Componente | Descrição |
|------------|-----------|
| `DsTable` | Tabela com hover |
| `DsBadge` | Pill badge |
| `DsAlert` | Alertas info/success/warning/error |
| `DsEmptyState` | Estado vazio |
| `DsModal` | Modal Vue (substitui Bootstrap modal) |
| `DsDropdown` | Menu dropdown Vue |

## Composables

| Arquivo | Descrição |
|---------|-----------|
| `useDsTheme.ts` | Temas de card (`blue`, `green`, `dark`, etc.) e delays de animação |
| `useDsModal.ts` | Controle open/close de modais |

## Padrão de página

```vue
<template>
  <div>
    <DsPageHeader title="..." subtitle="..." icon="..." />
    <DsPageShell>
      <!-- conteúdo -->
    </DsPageShell>
  </div>
</template>
```

## Convivência Bootstrap

- Tailwind com `preflight: false`
- Bootstrap Icons mantido via CDN
- Bootstrap CSS ainda carregado globalmente (legado); páginas migradas não usam classes Bootstrap
