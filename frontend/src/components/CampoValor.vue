<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { num, parseValor } from '../formato'

/**
 * Valor em reais no estilo dos apps de banco: só se digitam números e eles entram pela direita
 * (1 → 0,01 · 19347 → 193,47). Teclado numérico no celular, sem vírgula para errar.
 * Colar "1.234,56" ou "1234.56" também funciona.
 */
const modelo = defineModel<number | null>({ required: true })
const props = defineProps<{
  label?: string
  autofocus?: boolean
  disabled?: boolean
  /** Aceita zero e negativo (ex.: saldo inicial). */
  livre?: boolean
  /** Visual grande, usado no topo do formulário de lançamento. */
  grande?: boolean
}>()

const MAX_DIGITOS = 11
const negativo = ref((modelo.value ?? 0) < 0)
const centavos = ref(paraCentavos(modelo.value))
const input = ref<HTMLInputElement>()

function paraCentavos(v: number | null | undefined) {
  return v == null ? null : Math.round(Math.abs(v) * 100)
}

const texto = computed(() => (centavos.value == null ? '' : num(centavos.value / 100)))
const valido = computed(() => props.livre || (centavos.value ?? 0) > 0)

watch(modelo, (v) => {
  const c = paraCentavos(v)
  if (c !== centavos.value) centavos.value = c
  if (v != null && v !== 0) negativo.value = v < 0
})

function emitir() {
  modelo.value = centavos.value == null ? null : ((negativo.value ? -1 : 1) * centavos.value) / 100
}

/** Recalcula a partir dos dígitos do campo: funciona com qualquer teclado (inclusive Android). */
function aplicar(bruto: string, el: HTMLInputElement | null | undefined) {
  const digitos = bruto.replace(/\D/g, '').replace(/^0+/, '').slice(0, MAX_DIGITOS)
  centavos.value = digitos ? Number(digitos) : null
  emitir()
  nextTick(() => {
    if (!el) return
    el.value = texto.value // força a máscara mesmo se o texto final não mudou (ex.: digitou uma letra)
    el.setSelectionRange(el.value.length, el.value.length)
  })
}

const campo = ref()
const aoDigitar = (e: Event) => aplicar((e.target as HTMLInputElement).value, e.target as HTMLInputElement)
const aoAtualizar = (v: string) => aplicar(v ?? '', campo.value?.$el?.querySelector('input'))

function aoColar(e: ClipboardEvent) {
  const v = parseValor(e.clipboardData?.getData('text') ?? '')
  if (v == null) return
  e.preventDefault()
  centavos.value = Math.min(Math.round(Math.abs(v) * 100), 10 ** MAX_DIGITOS - 1)
  if (props.livre && v < 0) negativo.value = true
  emitir()
}

function alternarSinal() {
  negativo.value = !negativo.value
  emitir()
}

function focar(e: FocusEvent) {
  const el = e.target as HTMLInputElement
  requestAnimationFrame(() => el.setSelectionRange(el.value.length, el.value.length))
}

defineExpose({ focus: () => input.value?.focus() })
</script>

<template>
  <div v-if="grande" class="valor-grande" :class="{ 'valor-grande--vazio': centavos == null }">
    <span class="valor-grande__moeda">{{ negativo ? '−' : '' }}R$</span>
    <input
      ref="input"
      :value="texto"
      class="valor-grande__input"
      :size="Math.max(4, (texto || '0,00').length)"
      inputmode="numeric"
      placeholder="0,00"
      :aria-label="label ?? 'Valor'"
      :autofocus="autofocus"
      :disabled="disabled"
      autocomplete="off"
      @input="aoDigitar"
      @paste="aoColar"
      @focus="focar"
    />
  </div>

  <v-text-field
    v-else
    ref="campo"
    :model-value="texto"
    :label="label ?? 'Valor'"
    :prefix="negativo ? '−R$' : 'R$'"
    inputmode="numeric"
    placeholder="0,00"
    persistent-placeholder
    autocomplete="off"
    :autofocus="autofocus"
    :disabled="disabled"
    :rules="[() => valido || 'Informe um valor']"
    @update:model-value="aoAtualizar"
    @paste="aoColar"
    @focus="focar"
  >
    <template v-if="livre" #append-inner>
      <v-btn size="x-small" variant="tonal" :aria-label="negativo ? 'Tornar positivo' : 'Tornar negativo'" @click="alternarSinal">±</v-btn>
    </template>
  </v-text-field>
</template>

<style scoped>
.valor-grande {
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 8px;
  padding: 8px 0 4px;
}
.valor-grande__moeda {
  font-size: 1.25rem;
  font-weight: 600;
  opacity: 0.7;
}
.valor-grande__input {
  font: inherit;
  font-size: clamp(2.4rem, 9vw, 3.2rem);
  font-weight: 800;
  letter-spacing: -0.03em;
  color: inherit;
  background: transparent;
  border: 0;
  outline: none;
  width: auto;
  max-width: 100%;
  field-sizing: content;
  min-width: 3ch;
  text-align: left;
  font-variant-numeric: tabular-nums;
  caret-color: currentColor;
}
.valor-grande__input::placeholder {
  color: inherit;
  opacity: 0.35;
}
</style>
