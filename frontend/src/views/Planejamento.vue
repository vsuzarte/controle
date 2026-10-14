<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { api } from '../api'
import { alterado, avisar, erro, estado, verHistorico } from '../estado'
import { brl } from '../formato'
import type { CategoriaPlanejada, Planejamento } from '../tipos'
import CampoValor from '../components/CampoValor.vue'
import GraficoOrcamento from '../components/GraficoOrcamento.vue'
import LancamentoLinha from '../components/LancamentoLinha.vue'
import SeletorMes from '../components/SeletorMes.vue'

const p = ref<Planejamento | null>(null)
const carregando = ref(false)
const editando = ref<{ categoriaId: string; nome: string; valor: number | null } | null>(null)
const novaCategoria = ref<string | null>(null)

async function carregar() {
  carregando.value = true
  try {
    p.value = await api.get<Planejamento>(`/planejamento/${estado.mes}`)
  } catch (e) {
    erro(e)
  } finally {
    carregando.value = false
  }
}
onMounted(carregar)
watch(() => [estado.mes, estado.versao], carregar)

const url = (c: string) => `/planejamento/${estado.mes}/categorias/${c}`

async function sugerir(base: 'MesAnterior' | 'Media3Meses') {
  try {
    const r = await api.post<{ criados: number }>(`/planejamento/${estado.mes}/sugerir?base=${base}`)
    if (r.criados) alterado(`${r.criados} categoria(s) sugerida(s)`)
    else avisar('Nada novo para sugerir: as categorias com histórico já estão no orçamento.', 'info')
  } catch (e) {
    erro(e)
  }
}

async function salvarValor() {
  if (!editando.value || editando.value.valor == null) return
  try {
    await api.put(url(editando.value.categoriaId), { valor: editando.value.valor })
    editando.value = null
    alterado('Orçamento atualizado')
  } catch (e) {
    erro(e)
  }
}

async function dispensar(v: CategoriaPlanejada) {
  try {
    await api.post(url(v.categoriaId) + '/dispensar', {})
    alterado(`${v.categoria} removida da previsão deste mês`)
  } catch (e) {
    erro(e)
  }
}

async function restaurar(v: CategoriaPlanejada) {
  try {
    await api.post(url(v.categoriaId) + '/restaurar', {})
    alterado(`${v.categoria} voltou para a previsão`)
  } catch (e) {
    erro(e)
  }
}

function adicionar() {
  const c = estado.categorias.find((x) => x.id === novaCategoria.value)
  if (c) editando.value = { categoriaId: c.id, nome: c.nome, valor: null }
  novaCategoria.value = null
}

const categoriasDisponiveis = computed(() =>
  estado.categorias
    .filter((c) => c.ativa && c.tipo === 'Despesa' && !p.value?.variaveis.some((v) => v.categoriaId === c.id))
    .map((c) => ({ title: c.nome, value: c.id })),
)
</script>

<template>
  <div class="pagina">
    <div class="d-flex align-center flex-wrap ga-2 mb-3">
      <SeletorMes v-model="estado.mes" />
      <v-spacer />
      <v-menu>
        <template #activator="{ props }">
          <v-btn v-bind="props" variant="tonal" prepend-icon="mdi-auto-fix">Sugerir orçamento</v-btn>
        </template>
        <v-list density="compact">
          <v-list-item title="Com base no mês anterior" @click="sugerir('MesAnterior')" />
          <v-list-item title="Média dos últimos 3 meses" @click="sugerir('Media3Meses')" />
          <v-list-item subtitle="Só adiciona categorias que ainda não estão no orçamento; nada que você já ajustou é alterado." disabled />
        </v-list>
      </v-menu>
    </div>

    <v-progress-linear v-if="carregando && !p" indeterminate color="primary" />
    <template v-if="p">
      <v-card class="pa-4 mb-3">
        <v-row dense>
          <v-col cols="6" md="3"><div class="rotulo">Receitas</div><div class="valor text-success text-h6">{{ brl(p.totalReceitas) }}</div><div class="text-caption">{{ brl(p.receitasRecebidas) }} recebido</div></v-col>
          <v-col cols="6" md="3"><div class="rotulo">Fixas e parcelas</div><div class="valor text-h6">{{ brl(p.totalCompromissos) }}</div><div class="text-caption">{{ brl(p.compromissosPagos) }} pago</div></v-col>
          <v-col cols="6" md="3"><div class="rotulo">Variáveis</div><div class="valor text-h6">{{ brl(p.totalVariaveis) }}</div><div class="text-caption">{{ brl(p.gastoVariavel) }} gasto</div></v-col>
          <v-col cols="6" md="3">
            <div class="rotulo">Sobra no orçamento</div>
            <div class="valor text-h6" :class="p.resultadoPrevisto < 0 ? 'text-error' : 'text-primary'" data-testid="sobra-prevista">{{ brl(p.resultadoPrevisto) }}</div>
            <div class="text-caption">receitas − fixas − variáveis</div>
          </v-col>
        </v-row>
      </v-card>

      <v-row dense>
        <v-col cols="12" lg="7">
          <v-card class="mb-3">
            <v-card-title class="text-subtitle-1 font-weight-bold d-flex align-center">
              <v-icon class="mr-2">mdi-chart-bar</v-icon>Gastos do dia a dia por categoria
            </v-card-title>
            <v-card-subtitle class="text-wrap">
              Sugerido a partir do que você gastou antes (sem contar contas fixas e parcelas, que estão ao lado). Edite ou remova à vontade.
            </v-card-subtitle>
            <div v-if="p.variaveis.some((v) => !v.dispensado)" class="px-4 pt-2"><GraficoOrcamento :itens="p.variaveis" /></div>
            <v-list class="py-1">
              <div v-if="!p.variaveis.length" class="pa-4 text-medium-emphasis text-body-2">
                Ainda sem orçamento. Depois de um mês de lançamentos, o sistema sugere automaticamente; ou adicione abaixo.
              </div>
              <v-list-item v-for="v in p.variaveis" :key="v.categoriaId" :class="{ riscado: v.dispensado }">
                <template #prepend>
                  <v-avatar :color="v.cor ?? 'grey'" variant="tonal" size="34"><v-icon size="18">{{ v.icone ?? 'mdi-tag' }}</v-icon></v-avatar>
                </template>
                <v-list-item-title class="d-flex align-center ga-2">
                  {{ v.categoria }}
                  <v-chip v-if="v.orcado == null" size="x-small" variant="outlined">sem orçamento</v-chip>
                  <v-chip v-else-if="v.editado" size="x-small" variant="tonal">ajustado</v-chip>
                  <v-chip v-else size="x-small" variant="tonal" color="info">sugerido</v-chip>
                </v-list-item-title>
                <div v-if="!v.dispensado" class="mt-1">
                  <v-progress-linear
                    :model-value="v.orcado ? (v.gasto / v.orcado) * 100 : v.gasto > 0 ? 100 : 0"
                    :color="v.orcado != null && v.gasto > v.orcado ? 'error' : 'primary'"
                    height="6"
                    rounded
                    bg-opacity="0.15"
                  />
                  <div class="text-caption mt-1">
                    {{ brl(v.gasto) }} gasto
                    <template v-if="v.orcado != null"> de {{ brl(v.orcado) }}</template>
                    <span v-if="v.sugerido != null && v.editado" class="text-medium-emphasis"> · sugestão era {{ brl(v.sugerido) }}</span>
                  </div>
                </div>
                <template #append>
                  <v-btn v-if="!v.dispensado" icon="mdi-pencil" size="small" variant="text" aria-label="Editar valor"
                    @click="editando = { categoriaId: v.categoriaId, nome: v.categoria, valor: v.orcado }" />
                  <v-btn v-if="!v.dispensado && v.orcado != null" icon="mdi-close" size="small" variant="text" aria-label="Remover da previsão" @click="dispensar(v)" />
                  <v-btn v-if="v.dispensado" size="small" variant="tonal" @click="restaurar(v)">Restaurar</v-btn>
                </template>
              </v-list-item>
            </v-list>
            <div class="d-flex ga-2 pa-3 align-center">
              <v-autocomplete v-model="novaCategoria" :items="categoriasDisponiveis" label="Adicionar categoria ao orçamento" density="compact" @update:model-value="adicionar" />
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" lg="5">
          <v-card class="mb-3">
            <v-card-title class="text-subtitle-1 font-weight-bold"><v-icon class="mr-2">mdi-lock-outline</v-icon>Contas fixas e parcelas</v-card-title>
            <v-card-subtitle class="text-wrap">Já combinados: para mudar, edite o lançamento ou a conta em Contas do mês.</v-card-subtitle>
            <v-list class="py-0">
              <div v-if="!p.compromissos.length" class="pa-4 text-medium-emphasis text-body-2">Nenhum compromisso neste mês.</div>
              <LancamentoLinha v-for="l in p.compromissos" :key="l.id" :l="l" mostrar-data />
            </v-list>
          </v-card>

          <v-card>
            <v-card-title class="text-subtitle-1 font-weight-bold"><v-icon class="mr-2">mdi-cash-plus</v-icon>Receitas</v-card-title>
            <v-list class="py-0">
              <div v-if="!p.receitas.length" class="pa-4 text-medium-emphasis text-body-2">
                Nenhuma receita no mês. Cadastre o salário em Contas do mês para ele aparecer aqui todo mês.
              </div>
              <LancamentoLinha v-for="l in p.receitas" :key="l.id" :l="l" mostrar-data />
            </v-list>
          </v-card>
        </v-col>
      </v-row>

      <div class="text-center mt-2">
        <v-btn variant="text" size="small" prepend-icon="mdi-history" @click="verHistorico('', 'Últimas alterações')">Histórico de alterações</v-btn>
      </div>
    </template>

    <v-dialog :model-value="!!editando" max-width="380" @update:model-value="editando = null">
      <v-card v-if="editando">
        <v-card-title class="pt-4">Orçamento: {{ editando.nome }}</v-card-title>
        <v-card-text><CampoValor v-model="editando.valor" label="Quanto pretende gastar no mês" autofocus @keyup.enter="salvarValor" /></v-card-text>
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="editando = null">Voltar</v-btn>
          <v-btn color="primary" variant="flat" @click="salvarValor">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
