<script setup lang="ts">
import { ref, watch } from 'vue'
import { api, qs } from '../api'
import { erro, estado } from '../estado'
import { brl, dataBr, dataHoraBr } from '../formato'
import type { AuditLog } from '../tipos'

const itens = ref<AuditLog[]>([])
const carregando = ref(false)

watch(() => estado.historico.aberto, async (aberto) => {
  if (!aberto) return
  carregando.value = true
  try {
    itens.value = await api.get<AuditLog[]>('/auditoria' + qs({ entidadeId: estado.historico.entidadeId || null, limite: '200' }))
  } catch (e) {
    erro(e)
  } finally {
    carregando.value = false
  }
})

const ocultos = new Set(['Id', 'CriadoEm', 'AtualizadoEm', 'Sequencia', 'Programado', 'ContaId', 'ContaDestinoId', 'CategoriaId', 'RegraId', 'EstornoDeId'])
const nomes: Record<string, string> = {
  Valor: 'Valor', Data: 'Data', Descricao: 'Descrição', Status: 'Status', Observacao: 'Observação',
  FormaPagamento: 'Forma', FaturaMes: 'Fatura', MotivoCancelamento: 'Motivo', Ativa: 'Ativa', Nome: 'Nome',
  Dispensado: 'Removido da previsão', Editado: 'Editado', ValorSugerido: 'Sugerido', DataFim: 'Data final',
}

/** Mostra só o que mudou, em linguagem humana. */
function mudancas(a: AuditLog) {
  const antes = a.antes ? JSON.parse(a.antes) : {}
  const depois = a.depois ? JSON.parse(a.depois) : {}
  return Object.keys(depois)
    .filter((k) => !ocultos.has(k) && depois[k] !== null && depois[k] !== '' && (a.antes ? true : depois[k] !== false))
    .map((k) => ({ campo: nomes[k] ?? k, antes: fmt(k, antes[k]), depois: fmt(k, depois[k]), mudou: !!a.antes }))
}

function fmt(campo: string, v: unknown) {
  if (v === null || v === undefined) return '—'
  if (typeof v === 'boolean') return v ? 'sim' : 'não'
  if (campo.startsWith('Valor') && typeof v === 'number') return brl(v)
  if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v)) return dataBr(v)
  return String(v)
}
</script>

<template>
  <v-dialog v-model="estado.historico.aberto" max-width="620" scrollable>
    <v-card>
      <v-card-title class="pt-4 d-flex align-center">
        <v-icon class="mr-2">mdi-history</v-icon>
        {{ estado.historico.titulo || 'Histórico de alterações' }}
      </v-card-title>
      <v-card-text style="max-height: 70vh">
        <v-progress-linear v-if="carregando" indeterminate color="primary" />
        <div v-else-if="!itens.length" class="text-medium-emphasis">Nenhum registro.</div>
        <v-timeline v-else side="end" density="compact" truncate-line="both">
          <v-timeline-item v-for="a in itens" :key="a.id" size="x-small" dot-color="primary">
            <div class="d-flex flex-wrap ga-2 align-center">
              <b>{{ a.operacao }}</b>
              <span v-if="!estado.historico.entidadeId" class="text-caption">· {{ a.entidade }}</span>
              <span class="text-caption text-medium-emphasis">{{ dataHoraBr(a.dataHora) }}</span>
            </div>
            <div v-if="a.motivo" class="text-body-2 font-italic">“{{ a.motivo }}”</div>
            <div v-for="m in mudancas(a)" :key="m.campo" class="text-body-2">
              <span class="text-medium-emphasis">{{ m.campo }}:</span>
              <template v-if="m.mudou"> <span class="riscado">{{ m.antes }}</span> → </template>
              {{ m.depois }}
            </div>
          </v-timeline-item>
        </v-timeline>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn @click="estado.historico.aberto = false">Fechar</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
