<script setup lang="ts">
import { computed } from 'vue'
import { hojeIso, mesIso, nomeMes, somarMeses } from '../formato'

const mes = defineModel<string>({ required: true })
const atual = computed(() => mes.value === mesIso(hojeIso()))
</script>

<template>
  <v-sheet class="cartao d-inline-flex align-center pa-1" rounded="lg" border data-testid="seletor-mes">
    <v-btn icon="mdi-chevron-left" variant="text" size="small" aria-label="Mês anterior" @click="mes = somarMeses(mes, -1)" />
    <v-btn variant="text" class="font-weight-bold" min-width="160" @click="mes = mesIso(hojeIso())">
      {{ nomeMes(mes) }}
      <v-chip v-if="!atual" size="x-small" color="primary" variant="tonal" class="ml-2">hoje</v-chip>
    </v-btn>
    <v-btn icon="mdi-chevron-right" variant="text" size="small" aria-label="Próximo mês" @click="mes = somarMeses(mes, 1)" />
  </v-sheet>
</template>
