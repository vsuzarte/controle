<script setup lang="ts">
import { computed, nextTick, reactive, ref, watch } from 'vue'
import { useDisplay } from 'vuetify'
import { api } from '../api'
import { alterado, confirmarLancamento, contasAtivas, contasPagadoras, erro, estado } from '../estado'
import { brl, dataBr, hojeIso, nomeMes, somarDias, somarMeses } from '../formato'
import { FORMAS, TIPOS_CONTA, type FormaPagamento, type Lancamento, type StatusLancamento, type TipoLancamento } from '../tipos'
import CampoValor from './CampoValor.vue'
import { calcularParcelas, faltaNoVale as calcularFalta, faturaDaCompra, passaDoLimite as calcularLimite, type ModoParcela } from '../regras'

const { smAndDown } = useDisplay()
const salvando = ref(false)
const maisDetalhes = ref<boolean | null>(null)
const todasCategorias = ref(false)
const outraData = ref(false)
const pendentes = ref<Lancamento[]>([])
const tentouSalvar = ref(false)
const campoValor = ref<InstanceType<typeof CampoValor>>()

const f = reactive({
  tipo: 'Despesa' as TipoLancamento,
  valor: null as number | null,
  descricao: '',
  categoriaId: null as string | null,
  contaId: null as string | null,
  contaDestinoId: null as string | null,
  data: hojeIso(),
  pago: true,
  parcelas: 1,
  formaPagamento: 'Pix' as FormaPagamento | null,
  observacao: '',
  faturaMes: null as string | null,
  motivo: '',
  /** Compra no vale maior que o saldo: conta que paga o resto. */
  complementoId: null as string | null,
})

const editando = computed(() => estado.formulario.editar)

// ----- Parcelas: digitar o total (divide) ou o valor de cada parcela (multiplica) -----
const modoParcela = ref<ModoParcela>((() => { try { return localStorage.getItem('fin.parcela.modo') === 'parcela' ? 'parcela' : 'total' } catch { return 'total' } })())
watch(modoParcela, (v) => { try { localStorage.setItem('fin.parcela.modo', v) } catch { /* sem storage */ } })
const parcelado = computed(() => f.tipo === 'Despesa' && !editando.value && f.parcelas > 1)
const calculo = computed(() => (parcelado.value ? calcularParcelas(f.valor, f.parcelas, modoParcela.value) : null))
/** O que vai para o servidor: sempre o total da compra (ele divide em parcelas). */
const valorTotal = computed(() => calculo.value?.total ?? f.valor)
const rotuloValor = computed(() => (!parcelado.value ? 'Valor' : modoParcela.value === 'parcela' ? 'Valor da parcela' : 'Valor total'))
/** "1ª na fatura de nov/26, última em ago/27" (cartão) ou "1ª em 15/10/2026, última em ago/27". */
const quandoParcelas = computed(() => {
  if (!parcelado.value) return ''
  const c = conta.value
  const primeiro = c?.tipo === 'CartaoCredito' && c.diaFechamento && c.diaVencimento
    ? faturaDaCompra(f.data, c.diaFechamento, c.diaVencimento)
    : f.data.slice(0, 7)
  const ultimo = somarMeses(primeiro, f.parcelas - 1)
  return c?.tipo === 'CartaoCredito'
    ? `1ª na fatura de ${nomeMes(primeiro, true)}, última em ${nomeMes(ultimo, true)}`
    : `1ª em ${dataBr(f.data)}, última em ${nomeMes(ultimo, true)}`
})
const conta = computed(() => estado.contas.find((c) => c.id === f.contaId))
const ehCartao = computed(() => conta.value?.tipo === 'CartaoCredito')
const ehVale = computed(() => conta.value?.tipo === 'Beneficio')
/** Quanto falta no vale para esta compra (0 = cabe no saldo). */
const faltaNoVale = computed(() =>
  ehVale.value && f.tipo === 'Despesa' && !editando.value ? calcularFalta(f.valor, conta.value!.saldo) : 0,
)
const contasComplemento = computed(() => contasAtivas.value.filter((c) => c.tipo !== 'Beneficio'))
/** Compra no cartão acima do limite disponível: só avisa (o banco é quem recusa). */
const passaDoLimite = computed(() => {
  if (!ehCartao.value || editando.value || f.tipo !== 'Despesa') return null
  // Parcelado ocupa o limite inteiro na hora (como o banco faz).
  return calcularLimite(valorTotal.value, conta.value?.limite, conta.value?.limiteUsado)
})
const futura = computed(() => f.data > hojeIso())
const hoje = computed(() => hojeIso())
const ontem = computed(() => somarDias(hojeIso(), -1))

const categorias = computed(() =>
  estado.categorias.filter((c) => (c.ativa || c.id === f.categoriaId) && c.tipo === (f.tipo === 'Receita' ? 'Receita' : 'Despesa')),
)
const LIMITE_CATEGORIAS = 8
// A categoria escolhida sempre aparece, mesmo com a lista recolhida.
const categoriasVisiveis = computed(() => {
  if (todasCategorias.value || categorias.value.length <= LIMITE_CATEGORIAS + 1) return categorias.value
  const primeiras = categorias.value.slice(0, LIMITE_CATEGORIAS)
  const sel = categorias.value.find((c) => c.id === f.categoriaId)
  return sel && !primeiras.includes(sel) ? [sel, ...primeiras.slice(0, -1)] : primeiras
})
const contas = computed(() =>
  contasAtivas.value.filter((c) =>
    f.tipo === 'Despesa' ? true : f.tipo === 'Receita' ? c.tipo !== 'CartaoCredito' : c.tipo !== 'CartaoCredito' && c.tipo !== 'Beneficio'),
)
const destinos = computed(() => contasAtivas.value.filter((c) => c.id !== f.contaId && c.tipo !== 'Beneficio'))
const iconeConta = (tipo: string) => TIPOS_CONTA.find((t) => t.value === tipo)?.icone ?? 'mdi-bank'

const erros = computed(() => ({
  valor: !f.valor || f.valor <= 0 ? 'Digite o valor' : '',
  categoria: f.tipo !== 'Transferencia' && !f.categoriaId ? 'Escolha a categoria' : '',
  conta: !f.contaId ? 'Escolha a conta' : '',
  destino: f.tipo === 'Transferencia' && !f.contaDestinoId ? 'Escolha o destino' : '',
}))
const valido = computed(() => Object.values(erros.value).every((e) => !e))

/** Tocar de novo na opção escolhida não desmarca (o chip-group mandaria null). */
function manter(campo: 'categoriaId' | 'contaId' | 'contaDestinoId', v: unknown) {
  if (typeof v === 'string' && v) f[campo] = v
}

// Lembra a última conta/categoria por tipo: lançar vira "valor + toque" na maioria das vezes.
const memoria = (chave: string) => { try { return localStorage.getItem('fin.' + chave) } catch { return null } }
const lembrar = (chave: string, v: string | null) => { try { if (v) localStorage.setItem('fin.' + chave, v) } catch { /* sem storage */ } }

function preencher() {
  const l = estado.formulario.editar
  pendentes.value = []
  maisDetalhes.value = null
  todasCategorias.value = false
  tentouSalvar.value = false
  if (l) {
    Object.assign(f, {
      tipo: l.tipo, valor: Math.abs(l.valor), descricao: l.descricao, categoriaId: l.categoriaId,
      contaId: l.contaId, contaDestinoId: l.contaDestinoId, data: l.data, pago: l.status === 'Realizado',
      parcelas: 1, formaPagamento: l.formaPagamento, observacao: l.observacao ?? '', faturaMes: l.faturaMes, motivo: '',
    })
    outraData.value = l.data !== hoje.value && l.data !== ontem.value
    maisDetalhes.value = l.observacao ? true : null
    return
  }
  const tipo = estado.formulario.tipo
  const contaMem = memoria('conta.' + tipo)
  const catMem = memoria('categoria.' + tipo)
  Object.assign(f, {
    tipo, valor: null, descricao: '', parcelas: 1, observacao: '', faturaMes: null, motivo: '', pago: true,
    data: hojeIso(), contaDestinoId: null, complementoId: null,
    contaId: contasAtivas.value.some((c) => c.id === contaMem) ? contaMem : contasPagadoras.value[0]?.id ?? null,
    categoriaId: estado.categorias.some((c) => c.id === catMem && c.ativa) ? catMem : null,
    formaPagamento: (memoria('forma.' + tipo) as FormaPagamento) ?? 'Pix',
  })
  outraData.value = false
  if (!smAndDown.value) nextTick(() => setTimeout(() => campoValor.value?.focus(), 250))
}

watch(() => estado.formulario.aberto, (aberto) => { if (aberto) preencher() })

watch(() => f.tipo, (tipo, antigo) => {
  if (editando.value || tipo === antigo) return
  const cat = memoria('categoria.' + tipo)
  f.categoriaId = estado.categorias.some((c) => c.id === cat && c.tipo === (tipo === 'Receita' ? 'Receita' : 'Despesa')) ? cat : null
  if (tipo !== 'Despesa' && ehCartao.value) f.contaId = contasPagadoras.value[0]?.id ?? null
  if (tipo !== 'Despesa') f.parcelas = 1
  if (tipo === 'Transferencia' && ehVale.value) f.contaId = contasAtivas.value.find((c) => c.tipo !== 'CartaoCredito' && c.tipo !== 'Beneficio')?.id ?? null
})
watch(() => f.contaId, () => { f.complementoId = null; if (ehVale.value) f.parcelas = 1 })

watch(() => f.data, (d, antigo) => {
  // Mudou a data de uma compra no cartão: a fatura é recalculada pelo servidor.
  if (antigo && d !== antigo && d !== editando.value?.data) f.faturaMes = null
  if (!editando.value) f.pago = d <= hojeIso()
})

// Evita duplicidade: se há um previsto dessa categoria por perto, oferece confirmá-lo.
watch(() => [f.categoriaId, estado.formulario.aberto] as const, async ([cat, aberto]) => {
  pendentes.value = []
  if (!cat || !aberto || editando.value || f.tipo === 'Transferencia') return
  try {
    pendentes.value = await api.get<Lancamento[]>(`/lancamentos/pendentes?categoriaId=${cat}`)
  } catch { /* dica opcional */ }
})

function usarPendente(l: Lancamento) {
  estado.formulario.aberto = false
  confirmarLancamento(l, f.valor)
}

function descricaoPadrao() {
  if (f.descricao.trim()) return f.descricao.trim()
  if (f.tipo === 'Transferencia') return 'Transferência'
  return estado.categorias.find((c) => c.id === f.categoriaId)?.nome ?? ''
}

async function salvar(outro = false) {
  tentouSalvar.value = true
  if (!valido.value) return
  salvando.value = true
  try {
    const status: StatusLancamento | null = ehCartao.value ? null : f.pago ? 'Realizado' : 'Previsto'
    const corpo = {
      descricao: descricaoPadrao(),
      valor: valorTotal.value,
      data: f.data,
      contaId: f.contaId,
      contaDestinoId: f.tipo === 'Transferencia' ? f.contaDestinoId : null,
      categoriaId: f.tipo === 'Transferencia' ? null : f.categoriaId,
      formaPagamento: ehCartao.value ? 'CartaoCredito' : f.formaPagamento,
      observacao: f.observacao || null,
      faturaMes: f.faturaMes,
    }
    if (editando.value) {
      await api.put(`/lancamentos/${editando.value.id}`, { ...corpo, motivo: f.motivo || null })
      alterado('Lançamento corrigido')
    } else {
      await api.post('/lancamentos', {
        ...corpo, tipo: f.tipo, status, parcelas: f.tipo === 'Despesa' && f.parcelas > 1 ? f.parcelas : null,
        contaComplementoId: faltaNoVale.value > 0 ? f.complementoId : null,
      })
      lembrar('conta.' + f.tipo, f.contaId)
      lembrar('categoria.' + f.tipo, f.categoriaId)
      lembrar('forma.' + f.tipo, f.formaPagamento)
      alterado(f.parcelas > 1 ? `Compra lançada em ${f.parcelas}x (total ${brl(valorTotal.value ?? 0)})` : 'Lançamento salvo')
    }
    if (outro) {
      Object.assign(f, { valor: null, descricao: '', parcelas: 1, observacao: '' })
      pendentes.value = []
      tentouSalvar.value = false
      campoValor.value?.focus()
    } else {
      estado.formulario.aberto = false
    }
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}

const TIPOS: { valor: TipoLancamento; titulo: string; icone: string; cor: string }[] = [
  { valor: 'Despesa', titulo: 'Despesa', icone: 'mdi-arrow-down', cor: 'error' },
  { valor: 'Receita', titulo: 'Receita', icone: 'mdi-arrow-up', cor: 'success' },
  { valor: 'Transferencia', titulo: 'Transferir', icone: 'mdi-swap-horizontal', cor: 'secondary' },
]
const corTipo = computed(() => TIPOS.find((t) => t.valor === f.tipo)!.cor)
</script>

<template>
  <v-dialog v-model="estado.formulario.aberto" :fullscreen="smAndDown" max-width="540" scrollable>
    <v-card :rounded="smAndDown ? 0 : 'xl'">
      <v-card-item class="pb-0">
        <template #prepend>
          <v-btn icon="mdi-close" variant="text" size="small" aria-label="Fechar" @click="estado.formulario.aberto = false" />
        </template>
        <v-card-title class="text-center font-weight-bold">{{ editando ? 'Corrigir lançamento' : 'Novo lançamento' }}</v-card-title>
        <template #append><div style="width: 40px" /></template>
      </v-card-item>

      <div class="px-4 pt-2" :class="'text-' + corTipo">
        <div v-if="!editando" class="d-flex justify-center">
          <v-btn-toggle v-model="f.tipo" mandatory rounded="lg" density="comfortable" :color="corTipo" variant="tonal" divided>
            <v-btn v-for="t in TIPOS" :key="t.valor" :value="t.valor" :prepend-icon="t.icone">{{ t.titulo }}</v-btn>
          </v-btn-toggle>
        </div>
        <CampoValor ref="campoValor" v-model="f.valor" grande :label="rotuloValor" />
        <div v-if="parcelado" class="text-center text-caption font-weight-bold" data-testid="rotulo-valor">{{ rotuloValor }}</div>
        <div v-if="tentouSalvar && erros.valor" class="text-error text-caption text-center">{{ erros.valor }}</div>
      </div>

      <v-card-text class="pt-2">
        <!-- Categoria -->
        <template v-if="f.tipo !== 'Transferencia'">
          <div class="rotulo mb-1">Categoria</div>
          <v-chip-group :model-value="f.categoriaId" column selected-class="text-primary" aria-label="Categoria" @update:model-value="manter('categoriaId', $event)">
            <v-chip v-for="c in categoriasVisiveis" :key="c.id" :value="c.id" filter variant="tonal" rounded="lg">
              <v-icon start :color="f.categoriaId === c.id ? undefined : c.cor ?? undefined">{{ c.icone ?? 'mdi-tag' }}</v-icon>
              {{ c.nome }}
            </v-chip>
            <v-chip v-if="categorias.length > categoriasVisiveis.length" variant="text" rounded="lg" prepend-icon="mdi-dots-horizontal" @click.stop="todasCategorias = true">
              Mais {{ categorias.length - categoriasVisiveis.length }}
            </v-chip>
          </v-chip-group>
          <div v-if="tentouSalvar && erros.categoria" class="text-error text-caption">{{ erros.categoria }}</div>

          <v-alert
            v-for="p in pendentes"
            :key="p.id"
            type="info"
            variant="tonal"
            density="compact"
            rounded="lg"
            class="mt-2 text-body-2"
            icon="mdi-lightbulb-on-outline"
          >
            <b>{{ p.descricao }}</b> está prevista para {{ dataBr(p.data) }} ({{ brl(p.valor) }}). É esse pagamento?
            <div class="mt-2"><v-btn size="small" color="info" variant="flat" @click="usarPendente(p)">Sim, confirmar</v-btn></div>
          </v-alert>
        </template>

        <!-- Conta -->
        <div class="rotulo mb-1 mt-4">{{ f.tipo === 'Transferencia' ? 'De' : f.tipo === 'Receita' ? 'Entrou em' : 'Pago com' }}</div>
        <v-chip-group :model-value="f.contaId" column selected-class="text-primary" @update:model-value="manter('contaId', $event)" :aria-label="f.tipo === 'Transferencia' ? 'Conta de origem' : 'Conta'">
          <v-chip v-for="c in contas" :key="c.id" :value="c.id" filter variant="tonal" rounded="lg" :prepend-icon="iconeConta(c.tipo)">{{ c.nome }}</v-chip>
        </v-chip-group>
        <div v-if="tentouSalvar && erros.conta" class="text-error text-caption">{{ erros.conta }}</div>

        <template v-if="f.tipo === 'Transferencia'">
          <div class="rotulo mb-1 mt-4">Para</div>
          <v-chip-group :model-value="f.contaDestinoId" column selected-class="text-primary" aria-label="Conta de destino" @update:model-value="manter('contaDestinoId', $event)">
            <v-chip v-for="c in destinos" :key="c.id" :value="c.id" filter variant="tonal" rounded="lg" :prepend-icon="iconeConta(c.tipo)">{{ c.nome }}</v-chip>
          </v-chip-group>
          <div v-if="tentouSalvar && erros.destino" class="text-error text-caption">{{ erros.destino }}</div>
        </template>

        <v-alert v-if="ehCartao" variant="tonal" density="compact" rounded="lg" icon="mdi-credit-card-outline" class="mt-2 text-body-2">
          Vai para a fatura do cartão, na categoria escolhida. Pagar a fatura depois não conta como gasto de novo.
        </v-alert>
        <v-alert v-if="passaDoLimite !== null" type="warning" variant="tonal" density="compact" rounded="lg" class="mt-2 text-body-2" data-testid="aviso-limite">
          Passa do limite disponível do cartão ({{ brl(passaDoLimite) }}).
        </v-alert>
        <v-alert v-if="faltaNoVale > 0" type="warning" variant="tonal" density="compact" rounded="lg" class="mt-2 text-body-2" data-testid="aviso-vale">
          O {{ conta?.nome }} tem {{ brl(Math.max(0, conta?.saldo ?? 0)) }}. Completar os <b>{{ brl(faltaNoVale) }}</b> que faltam com:
          <v-chip-group v-model="f.complementoId" column selected-class="text-primary" aria-label="Completar com">
            <v-chip v-for="c in contasComplemento" :key="c.id" :value="c.id" filter variant="tonal" rounded="lg" size="small" :prepend-icon="iconeConta(c.tipo)">{{ c.nome }}</v-chip>
          </v-chip-group>
          <div class="text-caption">Sem escolher, a compra inteira fica no vale (o saldo dele fica negativo).</div>
        </v-alert>

        <!-- Data -->
        <div class="rotulo mb-1 mt-4">Data</div>
        <v-chip-group :model-value="outraData ? 'outra' : f.data" mandatory selected-class="text-primary" aria-label="Data">
          <v-chip :value="hoje" filter variant="tonal" rounded="lg" @click="f.data = hoje; outraData = false">Hoje</v-chip>
          <v-chip :value="ontem" filter variant="tonal" rounded="lg" @click="f.data = ontem; outraData = false">Ontem</v-chip>
          <v-chip value="outra" variant="tonal" rounded="lg" prepend-icon="mdi-calendar" @click="outraData = true">
            {{ outraData || (f.data !== hoje && f.data !== ontem) ? dataBr(f.data) : 'Outra data' }}
          </v-chip>
        </v-chip-group>
        <v-text-field v-if="outraData" v-model="f.data" type="date" label="Data" class="mt-2" style="max-width: 220px" />

        <!-- Parcelas -->
        <template v-if="f.tipo === 'Despesa' && !editando && !ehVale">
          <div class="rotulo mb-1 mt-4">Parcelas</div>
          <div class="d-flex align-center ga-3">
            <v-sheet rounded="lg" color="surface-variant" class="d-inline-flex align-center">
              <v-btn icon="mdi-minus" variant="text" aria-label="Menos parcelas" :disabled="f.parcelas <= 1" @click="f.parcelas = Math.max(1, f.parcelas - 1)" />
              <span class="font-weight-bold text-center" style="min-width: 36px" data-testid="parcelas">{{ f.parcelas }}x</span>
              <v-btn icon="mdi-plus" variant="text" aria-label="Mais parcelas" @click="f.parcelas = Math.min(120, f.parcelas + 1)" />
            </v-sheet>
            <span v-if="!parcelado" class="text-body-2 text-medium-emphasis">À vista</span>
            <v-btn-toggle v-else v-model="modoParcela" mandatory density="compact" variant="outlined" divided rounded="lg" color="primary" aria-label="O valor digitado é">
              <v-btn value="total" size="small">Valor total</v-btn>
              <v-btn value="parcela" size="small">Valor da parcela</v-btn>
            </v-btn-toggle>
          </div>
          <v-alert v-if="parcelado" type="info" variant="tonal" density="compact" rounded="lg" class="mt-2 text-body-2" icon="mdi-calculator-variant-outline" data-testid="resumo-parcelas">
            <template v-if="calculo">
              <div class="font-weight-bold">
                <template v-if="calculo.parcelas[0] === calculo.parcelas[1]">{{ f.parcelas }}x de {{ brl(calculo.parcelas[1]) }}</template>
                <template v-else>1x de {{ brl(calculo.parcelas[0]) }} + {{ f.parcelas - 1 }}x de {{ brl(calculo.parcelas[1]) }}</template>
                = {{ brl(calculo.total) }}
              </div>
              <div class="text-caption">{{ quandoParcelas }}</div>
            </template>
            <template v-else>Digite {{ modoParcela === 'parcela' ? 'o valor de cada parcela' : 'o valor total da compra' }}: o sistema calcula o resto.</template>
          </v-alert>
        </template>

        <!-- Já pagou? -->
        <template v-if="!ehCartao && f.tipo !== 'Transferencia' && !editando">
          <v-switch
            v-model="f.pago"
            :disabled="futura"
            color="success"
            hide-details
            inset
            density="compact"
            class="mt-3"
            :label="f.tipo === 'Receita' ? 'Já recebi' : 'Já paguei'"
          />
          <div v-if="!f.pago" class="text-caption text-medium-emphasis">
            Fica como <b>previsto</b>: aparece nos lembretes e só mexe no saldo quando você confirmar.
          </div>
        </template>

        <v-text-field
          v-model="f.descricao"
          label="Descrição (opcional)"
          :placeholder="descricaoPadrao() || 'Ex.: Mercado do mês'"
          persistent-placeholder
          class="mt-4"
        />

        <v-expansion-panels v-model="maisDetalhes" flat class="mt-2">
          <v-expansion-panel :value="true" bg-color="transparent">
            <v-expansion-panel-title class="px-1 text-primary font-weight-medium">Forma de pagamento e observação</v-expansion-panel-title>
            <v-expansion-panel-text>
              <v-chip-group v-if="!ehCartao && f.tipo !== 'Transferencia'" v-model="f.formaPagamento" column selected-class="text-primary">
                <v-chip v-for="x in FORMAS.filter((x) => x.value !== 'CartaoCredito')" :key="x.value" :value="x.value" filter variant="tonal" rounded="lg">
                  {{ x.title }}
                </v-chip>
              </v-chip-group>
              <v-textarea v-model="f.observacao" label="Observação" class="mt-2" />
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>

        <v-text-field v-if="editando" v-model="f.motivo" label="Motivo da correção (opcional)" class="mt-4" prepend-inner-icon="mdi-history" />
      </v-card-text>

      <v-divider />
      <v-card-actions class="flex-column pa-4 ga-1">
        <v-btn color="primary" variant="flat" size="x-large" block :loading="salvando" @click="salvar()">
          {{ editando ? 'Salvar correção' : 'Salvar' }}
        </v-btn>
        <v-btn v-if="!editando" variant="text" block :disabled="salvando" @click="salvar(true)">Salvar e lançar outro</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
