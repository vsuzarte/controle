<script setup lang="ts">
import { ref, watch } from 'vue'
import { num, parseValor } from '../formato'

const modelo = defineModel<number | null>({ required: true })
const props = defineProps<{ label?: string; autofocus?: boolean; disabled?: boolean; livre?: boolean }>()

const texto = ref(modelo.value != null ? num(modelo.value) : '')
let editando = false

watch(modelo, (v) => {
  if (!editando) texto.value = v != null ? num(v) : ''
})

function digitou(v: string) {
  editando = true
  texto.value = v
  modelo.value = parseValor(v)
  editando = false
}

function formatar() {
  if (modelo.value != null) texto.value = num(modelo.value)
}
</script>

<template>
  <v-text-field
    :model-value="texto"
    :label="label ?? 'Valor'"
    prefix="R$"
    inputmode="decimal"
    :autofocus="autofocus"
    :disabled="disabled"
    :rules="props.livre ? [] : [() => (modelo ?? 0) > 0 || 'Informe um valor']"
    @update:model-value="digitou"
    @blur="formatar"
    @focus="($event.target as HTMLInputElement).select()"
  />
</template>
