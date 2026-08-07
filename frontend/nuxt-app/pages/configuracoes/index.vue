<template>
  <div>
    <DsPageHeader title="Cadastros básicos" subtitle="Unidades, linguagens, especialidades e códigos universais" icon="gear">
      <template #actions>
        <DsButton v-if="auth.isAdmin" variant="secondary" size="sm" icon="database-gear" to="/configuracoes/firebird">Firebird</DsButton>
      </template>
    </DsPageHeader>
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-5">
        <DsButton v-for="c in catalogs" :key="c.key" size="sm" :variant="active === c.key ? 'primary' : 'secondary'" @click="select(c.key)">{{ c.label }}</DsButton>
      </div>
      <div class="flex gap-3 mb-4 items-end">
        <DsSearchInput v-model="search" class="flex-1" placeholder="Pesquisar no cadastro" @enter="load" />
        <DsButton v-if="auth.isAdmin" variant="success" icon="plus-lg" @click="openCreate">Novo</DsButton>
      </div>
      <DsAlert v-if="error" variant="error">{{ error }}</DsAlert>
      <div v-else-if="loading" class="text-center py-8 text-gray-500">Carregando...</div>
      <DsAlert v-else-if="!filtered.length" variant="info">Nenhum registro encontrado.</DsAlert>
      <DsTable v-else>
        <template #head><tr><th>Código</th><th>Descrição</th><th v-if="active==='unidades'">Status</th><th v-if="active==='codigos-universais'">Tipo / unidade</th><th /></tr></template>
        <tr v-for="row in filtered" :key="idOf(row)">
          <td>{{ idOf(row) }}</td><td><strong>{{ nameOf(row) }}</strong><small v-if="row.descricaoPtBr || (active==='codigos-universais' && row.descricao)" class="block text-gray-500">{{ row.descricaoPtBr || row.descricao }}</small></td>
          <td v-if="active==='unidades'">{{ row.status ? 'Ativa' : 'Inativa' }}</td><td v-if="active==='codigos-universais'">{{ row.tipoCodigo }}<span v-if="row.unidade"> · {{ row.unidade }}</span></td>
          <td><div v-if="auth.isAdmin" class="flex justify-end gap-2"><DsButton size="sm" variant="secondary" icon="pencil" @click="openEdit(row)">Editar</DsButton><DsButton size="sm" variant="danger" icon="trash" @click="remove(row)">Excluir</DsButton></div></td>
        </tr>
      </DsTable>
    </DsPageShell>
    <DsModal v-model="modal" :title="editing ? 'Editar registro' : 'Novo registro'" size="lg">
      <div v-if="active==='unidades'" class="space-y-3"><DsInput v-model="form.descricao" label="Descrição" required /><DsSelect v-model="form.status" label="Status"><option value="1">Ativa</option><option value="0">Inativa</option></DsSelect></div>
      <div v-else-if="active==='codigos-universais'" class="grid grid-cols-1 md:grid-cols-2 gap-3"><DsInput v-model="form.tipoCodigo" label="Tipo do código" required /><DsInput v-model="form.codigo" label="Código" required /><DsInput v-model="form.unidade" label="Unidade" /><DsInput v-model="form.descricaoPtBr" label="Descrição PT-BR" /><div class="md:col-span-2"><DsTextarea v-model="form.descricao" label="Descrição original" /></div></div>
      <DsInput v-else v-model="form.nome" :label="active==='linguagens' ? 'Linguagem' : 'Especialidade'" required />
      <template #footer><DsButton variant="secondary" @click="modal=false">Cancelar</DsButton><DsButton :disabled="saving" @click="save">{{ saving ? 'Salvando...' : 'Salvar' }}</DsButton></template>
    </DsModal>
  </div>
</template>
<script setup lang="ts">
definePageMeta({ layout: 'default' })
type Row = Record<string, any>
const api=useApi(), auth=useAuthStore(), swal=useSwal()
const catalogs=[{key:'unidades',label:'Unidades'},{key:'linguagens',label:'Linguagens'},{key:'especialidades',label:'Especialidades'},{key:'codigos-universais',label:'Códigos universais'}] as const
const active=ref<(typeof catalogs)[number]['key']>('unidades'), rows=ref<Row[]>([]), search=ref(''), loading=ref(false), saving=ref(false), error=ref(''), modal=ref(false), editing=ref<Row|null>(null)
const form=reactive<Row>({})
const filtered=computed(()=>active.value==='codigos-universais'||!search.value?rows.value:rows.value.filter(r=>nameOf(r).toLowerCase().includes(search.value.toLowerCase())))
const ids:Record<string,string>={unidades:'codUnidadeMedida',linguagens:'codLinguagem',especialidades:'codEspecialidade','codigos-universais':'codUniversal'}
function idOf(r:Row){return r[ids[active.value]]} function nameOf(r:Row){return active.value==='unidades'?r.descricao:active.value==='codigos-universais'?r.codigo:r.nome}
function select(k:any){active.value=k;search.value='';load()} function reset(){Object.keys(form).forEach(k=>delete form[k]);form.status='1'}
function openCreate(){editing.value=null;reset();modal.value=true} function openEdit(r:Row){editing.value=r;reset();Object.assign(form,r,{status:String(r.status??1)});modal.value=true}
async function load(){loading.value=true;error.value='';try{const q=active.value==='codigos-universais'&&search.value?`?search=${encodeURIComponent(search.value)}`:'';const res=await api.get<any>(`/api/web/cadastros/${active.value}${q}`);rows.value=res.data||[]}catch(e){error.value=e instanceof Error?e.message:'Erro ao carregar.'}finally{loading.value=false}}
function payload(){if(active.value==='unidades')return{descricao:form.descricao,status:Number(form.status)};if(active.value==='linguagens')return{nome:form.nome};if(active.value==='especialidades')return{nome:form.nome};return{tipoCodigo:form.tipoCodigo,codigo:form.codigo,descricao:form.descricao||null,unidade:form.unidade||null,descricaoPtBr:form.descricaoPtBr||null}}
async function save(){saving.value=true;try{const path=`/api/web/cadastros/${active.value}`;editing.value?await api.put(`${path}/${idOf(editing.value)}`,payload()):await api.post(path,payload());modal.value=false;await swal.toast('Registro salvo.');await load()}catch(e){await swal.toast(e instanceof Error?e.message:'Erro ao salvar.','error')}finally{saving.value=false}}
async function remove(r:Row){const c=await swal.confirm('Confirmar exclusão',`Excluir "${nameOf(r)}"?`);if(!c?.isConfirmed)return;try{await api.del(`/api/web/cadastros/${active.value}/${idOf(r)}`);await swal.toast('Registro excluído.');await load()}catch(e){await swal.toast(e instanceof Error?e.message:'Erro ao excluir.','error')}}
onMounted(load)
</script>
