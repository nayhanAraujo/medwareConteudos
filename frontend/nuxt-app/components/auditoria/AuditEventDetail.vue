<template>
  <dialog ref="dialog" class="audit-detail" aria-labelledby="audit-detail-title" @cancel.prevent="$emit('close')" @click="backdrop">
    <section class="flex flex-col max-h-[85vh]">
      <header class="flex items-center justify-between gap-4 border-b border-gray-100 px-5 py-4">
        <h2 id="audit-detail-title" class="text-lg font-semibold m-0">Detalhes do evento <span v-if="eventId" class="text-gray-500">#{{ eventId }}</span></h2>
        <DsButton variant="ghost" size="sm" icon="x-lg" aria-label="Fechar detalhes do evento" @click="$emit('close')" />
      </header>
      <div class="overflow-y-auto p-5 space-y-5">
        <p v-if="loading" class="py-8 text-center text-gray-500" role="status">Carregando evento...</p>
        <DsAlert v-else-if="error" variant="error">{{ error }}</DsAlert>
        <template v-else-if="detail">
          <div class="space-y-2">
            <div class="flex flex-wrap gap-2 items-center text-sm"><DsBadge>{{ label(detail.module) }}</DsBadge><DsBadge>{{ label(detail.action) }}</DsBadge><DsBadge :variant="resultVariant(detail.result)">{{ label(detail.result) }}</DsBadge></div>
            <p class="font-medium break-words">{{ detail.summary }}</p>
            <p class="text-sm text-gray-500">{{ formatAuditDate(detail.occurredAtUtc) }} · horário local</p>
          </div>
          <dl class="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
            <div><dt class="text-gray-500">Autor</dt><dd class="m-0 break-words">{{ detail.actorName || label(detail.actorKind) }}<span v-if="detail.actorId"> · #{{ detail.actorId }}</span></dd></div>
            <div><dt class="text-gray-500">Registro</dt><dd class="m-0 break-words">{{ detail.entityName || auditEntityLabels[detail.entityType || ''] || detail.entityType || '—' }}<span v-if="detail.entityId"> · #{{ detail.entityId }}</span><span v-if="detail.entityType" class="block text-xs text-gray-500 mt-1">{{ auditEntityLabels[detail.entityType] || detail.entityType }}</span></dd></div>
            <div><dt class="text-gray-500">Origem</dt><dd class="m-0 break-words">{{ label(detail.origin) }}</dd></div>
            <div><dt class="text-gray-500">IP</dt><dd class="m-0 break-all">{{ detail.ipAddress || 'Não registrado' }}</dd></div>
            <div class="sm:col-span-2"><dt class="text-gray-500">Código de correlação</dt><dd class="m-0 break-all select-all">{{ detail.correlationId || 'Não registrado' }}</dd></div>
            <div v-if="detail.operationId" class="sm:col-span-2"><dt class="text-gray-500">Operação</dt><dd class="m-0 break-all select-all">{{ detail.operationId }}</dd></div>
            <div v-if="detail.requestPath" class="sm:col-span-2"><dt class="text-gray-500">Requisição</dt><dd class="m-0 break-all font-mono text-xs">{{ detail.requestMethod }} {{ detail.requestPath }}</dd></div>
            <div v-if="detail.reasonCode"><dt class="text-gray-500">Motivo</dt><dd class="m-0 break-words">{{ detail.reasonCode }}</dd></div>
          </dl>
          <section class="border-t border-gray-100 pt-4" aria-labelledby="audit-diff-title">
            <h3 id="audit-diff-title" class="text-base font-semibold mb-3">Campos alterados</h3>
            <p v-if="!detail.changes.length" class="text-sm text-gray-500">Este evento não possui diferenças de campos. Senhas e conteúdo de arquivos não são registrados.</p>
            <div v-else class="space-y-3">
              <article v-for="(change, index) in detail.changes" :key="`${change.field}-${index}`" class="rounded-xl border border-gray-200 overflow-hidden">
                <h4 class="text-sm font-semibold px-4 py-2 bg-gray-50 break-words m-0">{{ change.field }}</h4>
                <div class="grid grid-cols-1 sm:grid-cols-2 text-sm">
                  <div class="p-4 border-b sm:border-b-0 sm:border-r border-gray-100"><p class="text-xs text-gray-500 mb-2">Antes</p><pre class="m-0 whitespace-pre-wrap break-words font-sans text-sm">{{ formatAuditValue(change.before) }}</pre></div>
                  <div class="p-4"><p class="text-xs text-gray-500 mb-2">Depois</p><pre class="m-0 whitespace-pre-wrap break-words font-sans text-sm">{{ formatAuditValue(change.after) }}</pre></div>
                </div>
              </article>
            </div>
          </section>
          <p v-if="detail.result === 'iniciado'" class="text-xs text-gray-500">O servidor disponibilizou o arquivo. Este evento não confirma que o navegador concluiu o download.</p>
        </template>
      </div>
      <footer class="border-t border-gray-100 px-5 py-3 flex justify-end"><DsButton variant="secondary" @click="$emit('close')">Fechar</DsButton></footer>
    </section>
  </dialog>
</template>

<script setup lang="ts">
import { auditLabels, auditEntityLabels, formatAuditDate, formatAuditValue, type AuditDetail } from '~/utils/auditoria'
const props = defineProps<{ eventId: string | null; detail: AuditDetail | null; loading: boolean; error: string }>()
const emit = defineEmits<{ close: [] }>()
const dialog = ref<HTMLDialogElement | null>(null)
const label = (value: string) => auditLabels[value] || value
const resultVariant = (value: string) => value === 'sucesso' ? 'success' : value === 'falha' ? 'danger' : value === 'negado' ? 'warning' : 'neutral'
function sync() {
  if (props.eventId && !dialog.value?.open) dialog.value?.showModal()
  else if (!props.eventId && dialog.value?.open) dialog.value.close()
}
function backdrop(event: MouseEvent) { if (event.target === dialog.value) emit('close') }
watch(() => props.eventId, () => nextTick(sync))
onMounted(sync)
onBeforeUnmount(() => dialog.value?.close())
</script>

<style scoped>
.audit-detail { position: fixed; inset: 0; margin: auto; width: min(56rem, calc(100vw - 2rem)); max-width: none; max-height: 85vh; padding: 0; border: 1px solid #e5e7eb; border-radius: 20px; color: #111827; background: white; box-shadow: 0 18px 60px rgb(15 23 42 / 0.2); }
.audit-detail::backdrop { background: rgb(15 23 42 / 0.3); }
pre { overflow-wrap: anywhere; }
</style>
