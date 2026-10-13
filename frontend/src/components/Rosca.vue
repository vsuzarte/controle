<script setup lang="ts">
import { computed } from 'vue'
import { Doughnut } from 'vue-chartjs'
import { ArcElement, Chart as ChartJS, Tooltip, type ChartOptions } from 'chart.js'
import { brl } from '../formato'

ChartJS.register(ArcElement, Tooltip)

const props = defineProps<{ itens: { nome: string; valor: number; cor: string }[]; titulo?: string }>()

const total = computed(() => props.itens.reduce((s, i) => s + Math.max(0, i.valor), 0))
const dados = computed(() => ({
  labels: props.itens.map((i) => i.nome),
  datasets: [{ data: props.itens.map((i) => i.valor), backgroundColor: props.itens.map((i) => i.cor), borderWidth: 0, hoverOffset: 6, borderRadius: 6, spacing: 2 }],
}))
const opcoes: ChartOptions<'doughnut'> = {
  cutout: '72%',
  responsive: true,
  maintainAspectRatio: true,
  plugins: {
    legend: { display: false },
    tooltip: { callbacks: { label: (c) => ` ${c.label}: ${brl(c.parsed)}` } },
  },
}
</script>

<template>
  <div class="rosca" role="img" :aria-label="titulo ?? 'Distribuição'">
    <Doughnut :data="dados" :options="opcoes" />
    <div class="rosca__centro">
      <div class="rotulo">Total</div>
      <div class="valor">{{ brl(total) }}</div>
    </div>
  </div>
</template>

<style scoped>
.rosca { position: relative; width: 190px; height: 190px; flex-shrink: 0; }
.rosca__centro { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; pointer-events: none; }
</style>
