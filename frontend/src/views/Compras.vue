<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { api, qs } from '../api'
import { erro, estado, lerNotaFiscal, verCompra } from '../estado'
import { brl, dataBr, dataHoraBr, hojeIso } from '../formato'
import type { CompraResumo, Produto } from '../tipos'

/** Busca nos itens das notas importadas: quanto gastou com cada produto, quantas vezes comprou e onde estava mais barato. */
const busca = ref('')
const periodo = ref<'3' | '6' | '12' | '24'>('12')
const produtos = ref<Produto[]>([])
const compras = ref<CompraResumo[]>([])
const carregando = ref(false)

const de = computed(() => {
  const d = new Date(hojeIso() + 'T12:00:00')
  d.setMonth(d.getMonth() - Number(periodo.value))
  return d.toISOString().slice(0, 10)
})

async function carregar() {
  carregando.value = true
  try {
    ;[produtos.value, compras.value] = await Promise.all([
      api.get<Produto[]>('/compras/produtos' + qs({ busca: busca.value || null, de: de.value, ate: hojeIso() })),
      api.get<CompraResumo[]>('/compras?limite=30'),
    ])
  } catch (e) {
    erro(e)
  } finally {
    carregando.value = false
  }
}
onMounted(carregar)
let espera: ReturnType<typeof setTimeout>
watch(busca, () => { clearTimeout(espera); espera = setTimeout(carregar, 300) })
watch([periodo, () => estado.versao], carregar)

const total = computed(() => produtos.value.reduce((s, p) => s + p.total, 0))
const qtd = (p: Produto) => `${p.quantidade.toLocaleString('pt-BR', { maximumFractionDigits: 3 })} ${p.unidade?.toLowerCase() ?? ''}`.trim()
</script>

<template>
  <div class="pagina">
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <v-text-field v-model="busca" prepend-inner-icon="mdi-magnify" placeholder="Buscar produto (ex.: coca, leite, sabão)" density="compact"
        hide-details clearable autofocus style="min-width: 240px; max-width: 420px" data-testid="busca-produto" />
      <v-btn-toggle v-model="periodo" mandatory density="compact" variant="outlined" divided rounded="lg" aria-label="Período">
        <v-btn value="3" size="small">3 meses</v-btn>
        <v-btn value="6" size="small">6 meses</v-btn>
        <v-btn value="12" size="small">1 ano</v-btn>
        <v-btn value="24" size="small">2 anos</v-btn>
      </v-btn-toggle>
      <v-chip v-if="produtos.length" variant="tonal" color="error" label>
        <span class="text-caption mr-2">{{ busca ? 'Gasto com a busca' : 'Gasto em compras' }}</span><span class="valor">{{ brl(total) }}</span>
      </v-chip>
      <v-spacer />
      <v-btn color="primary" variant="flat" prepend-icon="mdi-qrcode-scan" @click="lerNotaFiscal()">Lançar nota</v-btn>
    </div>
    <v-progress-linear v-if="carregando" indeterminate color="primary" class="mb-2" />

    <v-row>
      <!-- ===== Produtos ===== -->
      <v-col cols="12" lg="8">
        <v-card>
          <div class="d-flex align-center ga-3 px-4 py-3 cabeca">
            <v-avatar color="primary" variant="tonal" rounded="lg" size="34"><v-icon size="18">mdi-basket-outline</v-icon></v-avatar>
            <div class="font-weight-bold">Produtos</div>
            <span class="text-caption text-medium-emphasis">{{ produtos.length }} · notas e lançamentos, do que mais gastou para o que menos</span>
          </div>
          <div v-if="!produtos.length && !carregando" class="pa-8 text-center text-medium-emphasis">
            <template v-if="busca">Nada com "{{ busca }}" nesse período.</template>
            <template v-else>Suas compras aparecem aqui: as lançadas à mão e, com mais detalhe, as lançadas pela nota fiscal.</template>
          </div>
          <div v-for="p in produtos" :key="p.descricao" class="linha d-flex flex-wrap align-center ga-3 px-4 py-2" data-testid="produto">
            <div class="flex-grow-1" style="min-width: 200px">
              <div class="font-weight-medium d-flex align-center ga-1">
                <v-icon size="14" class="text-medium-emphasis" :title="p.origem === 'Nota' ? 'Item de nota fiscal' : 'Lançado à mão'">
                  {{ p.origem === 'Nota' ? 'mdi-receipt-text-outline' : 'mdi-pencil-outline' }}
                </v-icon>
                {{ p.descricao }}
              </div>
              <div v-if="p.origem === 'Lancamento'" class="text-caption text-medium-emphasis">
                {{ p.vezes }}× · média {{ brl(p.precoMedio) }} · último {{ brl(p.ultimoPreco) }} em {{ dataBr(p.ultimaCompra).slice(0, 5) }} ({{ p.ultimaLoja }})
              </div>
              <div v-else class="text-caption text-medium-emphasis">
                {{ p.vezes }}× · {{ qtd(p) }} · média {{ brl(p.precoMedio) }}/{{ p.unidade?.toLowerCase() ?? 'un' }}
                · último {{ brl(p.ultimoPreco) }} em {{ dataBr(p.ultimaCompra).slice(0, 5) }} ({{ p.ultimaLoja }})
                <template v-if="p.vezes > 1 && p.menorPreco < p.ultimoPreco"> · <span class="text-success">mais barato {{ brl(p.menorPreco) }} em {{ p.lojaMenorPreco }}</span></template>
              </div>
            </div>
            <div class="valor">{{ brl(p.total) }}</div>
          </div>
        </v-card>
      </v-col>

      <!-- ===== Notas lançadas ===== -->
      <v-col cols="12" lg="4">
        <v-card>
          <div class="d-flex align-center ga-3 px-4 py-3 cabeca">
            <v-avatar color="secondary" variant="tonal" rounded="lg" size="34"><v-icon size="18">mdi-receipt-text-outline</v-icon></v-avatar>
            <div class="font-weight-bold">Notas lançadas</div>
          </div>
          <div v-if="!compras.length" class="pa-6 text-center text-medium-emphasis text-body-2">Nenhuma ainda.</div>
          <div v-for="c in compras" :key="c.id" class="linha d-flex align-center ga-3 px-4 py-2 clicavel" :aria-label="c.loja" @click="verCompra(c.id)">
            <div class="flex-grow-1" style="min-width: 0">
              <div class="font-weight-medium text-truncate">{{ c.loja }}</div>
              <div class="text-caption text-medium-emphasis">{{ dataHoraBr(c.emissao) }} · {{ c.itens }} itens · {{ c.conta }}</div>
            </div>
            <div class="valor">{{ brl(c.total) }}</div>
          </div>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<style scoped>
.cabeca { border-bottom: 1px solid rgba(var(--v-border-color), 0.16); }
.linha { border-bottom: 1px solid rgba(var(--v-border-color), 0.12); }
.linha:nth-child(even) { background: rgba(var(--v-theme-on-surface), 0.025); }
.linha:last-child { border-bottom: 0; }
.clicavel { cursor: pointer; }
.clicavel:hover { background: rgba(var(--v-theme-primary), 0.08); }
</style>
