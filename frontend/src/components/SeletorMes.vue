<script setup lang="ts">
import { computed } from 'vue'
import { hojeIso, mesIso, nomeMes, somarMeses } from '../formato'

const mes = defineModel<string>({ required: true })
const atual = computed(() => mes.value === mesIso(hojeIso()))
</script>

<template>
  <div class="d-flex align-center ga-1">
    <v-btn icon="mdi-chevron-left" variant="text" density="comfortable" aria-label="Mês anterior" @click="mes = somarMeses(mes, -1)" />
    <div class="text-subtitle-1 font-weight-medium text-center" style="min-width: 150px">{{ nomeMes(mes) }}</div>
    <v-btn icon="mdi-chevron-right" variant="text" density="comfortable" aria-label="Próximo mês" @click="mes = somarMeses(mes, 1)" />
    <v-btn v-if="!atual" variant="tonal" size="small" @click="mes = mesIso(hojeIso())">Hoje</v-btn>
  </div>
</template>
