<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useDisplay } from 'vuetify'
import { api } from '../api'
import { alterado, contasAtivas, erro, estado, verCompra } from '../estado'
import { brl, dataHoraBr } from '../formato'
import { cameraDisponivel, lerQrDaImagem } from '../qrcode'
import LeitorQrCamera from './LeitorQrCamera.vue'
import type { LeituraNota } from '../tipos'

/**
 * Lançar uma compra pela nota fiscal: link ou foto do QR code → conferir (loja, conta, categoria de cada item) → lançar.
 * Vira um lançamento por categoria; os itens ficam guardados para a busca "O que comprei".
 */
const { smAndDown } = useDisplay()
const link = ref('')
const lendo = ref(false)
const salvando = ref(false)
const nota = ref<LeituraNota | null>(null)
const loja = ref('')
const contaId = ref<string | null>(null)
const categorias = ref<Record<number, string>>({})
const foto = ref<HTMLInputElement | null>(null)
/** Câmera ao vivo aberta (abre sozinha ao entrar, quando a conexão permite). */
const camera = ref(false)

watch(() => estado.notaFiscal.aberto, (aberto) => {
  if (!aberto) return
  link.value = ''
  nota.value = null
  camera.value = cameraDisponivel()
})

function lidoNaCamera(texto: string) {
  camera.value = false
  link.value = texto
  ler(texto)
}

const opcoesCategoria = computed(() => estado.categorias.filter((c) => c.tipo === 'Despesa' && c.ativa)
  .map((c) => ({ title: c.nome, value: c.id, cor: c.cor, icone: c.icone })))
const opcoesConta = computed(() => contasAtivas.value.map((c) => ({ title: c.nome, value: c.id })))
const nomeCategoria = (id: string) => estado.categorias.find((c) => c.id === id)?.nome ?? '?'

/** Quanto vai para cada categoria (cada uma vira um lançamento). */
const porCategoria = computed(() => {
  const soma = new Map<string, number>()
  for (const i of nota.value?.itens ?? []) {
    const cat = categorias.value[i.sequencia]
    soma.set(cat, Math.round(((soma.get(cat) ?? 0) + i.valorPago) * 100) / 100)
  }
  return [...soma.entries()].map(([id, valor]) => ({ id, nome: nomeCategoria(id), valor })).sort((a, b) => b.valor - a.valor)
})

async function ler(url = link.value) {
  if (!url.trim()) return
  lendo.value = true
  try {
    const lida = await api.post<LeituraNota>('/compras/ler', { url })
    nota.value = lida
    link.value = lida.url
    loja.value = lida.loja
    contaId.value = lida.contaSugeridaId
    categorias.value = Object.fromEntries(lida.itens.map((i) => [i.sequencia, i.categoriaId]))
  } catch (e) {
    erro(e)
  } finally {
    lendo.value = false
  }
}

async function aoTirarFoto(evento: Event) {
  const arquivo = (evento.target as HTMLInputElement).files?.[0]
  if (!arquivo) return
  lendo.value = true
  try {
    const texto = await lerQrDaImagem(arquivo)
    if (!texto) throw new Error('Não achei o QR code na foto. Tente de novo mais perto, com o QR inteiro e bem iluminado.')
    link.value = texto
    await ler(texto)
  } catch (e) {
    erro(e)
  } finally {
    lendo.value = false
    if (foto.value) foto.value.value = ''
  }
}

function tudoComo(categoriaId: string | null) {
  if (!categoriaId || !nota.value) return
  categorias.value = Object.fromEntries(nota.value.itens.map((i) => [i.sequencia, categoriaId]))
}

async function lancar() {
  if (!nota.value || !contaId.value) return erro('Escolha com que conta você pagou.')
  salvando.value = true
  try {
    await api.post('/compras', {
      url: nota.value.url,
      contaId: contaId.value,
      loja: loja.value,
      itens: nota.value.itens.map((i) => ({ sequencia: i.sequencia, categoriaId: categorias.value[i.sequencia] })),
    })
    estado.notaFiscal.aberto = false
    alterado(`Compra lançada: ${nota.value.itens.length} itens em ${porCategoria.value.length} lançamento(s)`)
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}
</script>

<template>
  <v-dialog v-model="estado.notaFiscal.aberto" :fullscreen="smAndDown" max-width="920" scrollable>
    <v-card>
      <div class="d-flex align-center ga-2 px-4 pt-3">
        <v-icon color="primary">mdi-qrcode-scan</v-icon>
        <div class="font-weight-bold text-h6">Lançar pela nota fiscal</div>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" aria-label="Fechar" @click="estado.notaFiscal.aberto = false" />
      </div>

      <!-- ===== 1. Link ou foto do QR code ===== -->
      <div class="d-flex flex-wrap align-center ga-2 px-4 py-3">
        <v-text-field v-model="link" placeholder="Cole o link do QR code da nota" density="compact" hide-details clearable
          prepend-inner-icon="mdi-link-variant" style="min-width: 240px" data-testid="link-nota" @keyup.enter="ler()" />
        <v-btn color="primary" variant="tonal" height="40" :loading="lendo && !!link" :disabled="!link" @click="ler()">Ler</v-btn>
        <v-btn color="primary" variant="flat" height="40" prepend-icon="mdi-qrcode-scan" data-testid="abrir-camera" @click="camera = !camera">Câmera</v-btn>
        <v-btn color="primary" variant="tonal" height="40" prepend-icon="mdi-camera" :loading="lendo && !link" @click="foto?.click()">Foto do QR</v-btn>
        <input ref="foto" type="file" accept="image/*" capture="environment" class="d-none" @change="aoTirarFoto" />
      </div>
      <LeitorQrCamera v-if="camera" @lido="lidoNaCamera" @fechar="camera = false" />
      <v-divider />

      <v-card-text v-if="!nota && !camera" class="text-body-2 text-medium-emphasis">
        Aponte a câmera para o QR code da nota, tire uma foto dele ou cole o link que ele abre. Os itens aparecem aqui para você conferir antes de lançar.
        Por enquanto, só notas de SP.
      </v-card-text>

      <!-- ===== 2. Conferir ===== -->
      <template v-if="nota">
        <v-card-text class="pa-0">
          <v-alert v-if="nota.compraExistenteId" type="info" variant="tonal" density="compact" class="ma-3">
            Esta nota já foi lançada.
            <template #append><v-btn variant="text" size="small" @click="verCompra(nota.compraExistenteId!)">Ver compra</v-btn></template>
          </v-alert>

          <div class="d-flex flex-wrap align-center ga-2 pa-3">
            <v-text-field v-model="loja" label="Loja" density="compact" hide-details style="min-width: 220px" :hint="nota.lojaNaNota" />
            <v-select v-model="contaId" :items="opcoesConta" label="Pago com" density="compact" hide-details style="min-width: 200px" data-testid="pago-com-nota" />
            <v-chip variant="tonal" label>{{ dataHoraBr(nota.emissao) }}</v-chip>
            <v-chip v-if="nota.formaPagamento" variant="tonal" label prepend-icon="mdi-credit-card-outline">{{ nota.formaPagamento }}</v-chip>
          </div>

          <div class="d-flex flex-wrap align-center ga-2 px-3 pb-2">
            <v-btn size="small" variant="tonal" prepend-icon="mdi-cart" :disabled="!nota.categoriaPadraoId" @click="tudoComo(nota.categoriaPadraoId)">
              Tudo como {{ nota.categoriaPadraoId ? nomeCategoria(nota.categoriaPadraoId) : 'Mercado' }}
            </v-btn>
            <v-spacer />
            <v-chip v-for="c in porCategoria" :key="c.id" size="small" variant="outlined" label>{{ c.nome }} · {{ brl(c.valor) }}</v-chip>
          </div>

          <div class="itens" data-testid="itens-nota">
            <div v-for="i in nota.itens" :key="i.sequencia" class="item d-flex flex-wrap align-center ga-2 px-3 py-2">
              <div class="flex-grow-1" style="min-width: 180px">
                <div class="font-weight-medium">{{ i.descricao }}</div>
                <div class="text-caption text-medium-emphasis">
                  {{ i.quantidade.toLocaleString('pt-BR', { maximumFractionDigits: 3 }) }} {{ i.unidade?.toLowerCase() }} × {{ brl(i.valorUnitario) }}
                  <template v-if="i.valorPago !== i.valorTotal"> · {{ brl(i.valorTotal) }} sem desconto</template>
                </div>
              </div>
              <v-select v-model="categorias[i.sequencia]" :items="opcoesCategoria" density="compact" hide-details variant="outlined"
                style="max-width: 220px; min-width: 170px" :aria-label="'Categoria de ' + i.descricao" />
              <div class="valor text-right" style="min-width: 86px">{{ brl(i.valorPago) }}</div>
            </div>
          </div>
        </v-card-text>
        <v-divider />
        <div class="d-flex flex-wrap align-center ga-3 px-4 py-3">
          <div class="text-body-2">
            {{ nota.itens.length }} itens
            <template v-if="nota.desconto"> · {{ brl(nota.valorTotal) }} − desconto {{ brl(nota.desconto) }}</template>
          </div>
          <v-spacer />
          <div class="medio">{{ brl(nota.valorAPagar) }}</div>
          <v-btn color="primary" variant="flat" size="large" :loading="salvando" :disabled="!!nota.compraExistenteId || !contaId" data-testid="lancar-nota" @click="lancar">
            Lançar compra
          </v-btn>
        </div>
      </template>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.item { border-bottom: 1px solid rgba(var(--v-border-color), 0.12); }
.item:nth-child(even) { background: rgba(var(--v-theme-on-surface), 0.03); }
</style>
