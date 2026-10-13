<script setup lang="ts">
import { computed } from 'vue'
import { Bar } from 'vue-chartjs'
import { BarElement, CategoryScale, Chart as ChartJS, Legend, LinearScale, Tooltip, type ChartOptions } from 'chart.js'
import { useTheme } from 'vuetify'
import { brl } from '../formato'
import type { CategoriaPlanejada } from '../tipos'

ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend)

const props = defineProps<{ itens: CategoriaPlanejada[] }>()
const tema = useTheme()

const dados = computed(() => {
  const itens = props.itens.filter((v) => !v.dispensado)
  const cores = tema.current.value.colors
  return {
    labels: itens.map((v) => v.categoria),
    datasets: [
      { label: 'Orçado', data: itens.map((v) => v.orcado ?? 0), backgroundColor: cores.secondary + '40', borderRadius: 8, barPercentage: 0.7 },
      {
        label: 'Gasto',
        data: itens.map((v) => v.gasto),
        backgroundColor: itens.map((v) => (v.orcado != null && v.gasto > v.orcado ? cores.error : cores.primary)),
        borderRadius: 8,
        barPercentage: 0.7,
      },
    ],
  }
})

const opcoes = computed<ChartOptions<'bar'>>(() => {
  const texto = tema.current.value.dark ? '#A3B1C2' : '#64748B'
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom', labels: { color: texto, usePointStyle: true, pointStyle: 'rectRounded' } },
      tooltip: { callbacks: { label: (c) => ` ${c.dataset.label}: ${brl(c.parsed.y)}` } },
    },
    scales: {
      x: { grid: { display: false }, ticks: { color: texto } },
      y: { grid: { color: tema.current.value.dark ? 'rgba(255,255,255,0.06)' : 'rgba(15,23,42,0.06)' }, ticks: { color: texto, callback: (v) => brl(Number(v)).replace(',00', '') }, border: { display: false } },
    },
  }
})
</script>

<template>
  <div style="height: 240px">
    <Bar :data="dados" :options="opcoes" />
  </div>
</template>
