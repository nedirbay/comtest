<script setup lang="ts">
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { init, use, type ECharts } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { theme } from '@/app/theme'

use([BarChart, GridComponent, TooltipComponent, CanvasRenderer])

const props = defineProps<{ days: { label: string; count: number }[] }>()

const { t } = useI18n({ useScope: 'global' })
const host = ref<HTMLDivElement | null>(null)
let chart: ECharts | null = null
let observer: ResizeObserver | null = null

function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
}

function render(): void {
  if (!chart) {
    return
  }

  // Dark mode takes its own step of the ramp. Both were run through the contrast validator.
  const bar = token(theme.isDark.value ? '--color-brand-400' : '--color-brand-500')
  const ink = token('--theme-ink')
  const muted = token('--theme-ink-muted')
  const line = token('--theme-line')
  const surface = token('--theme-surface-raised')

  chart.setOption({
    animation: false,
    grid: { left: 4, right: 4, top: 12, bottom: 4, containLabel: true },
    tooltip: {
      trigger: 'item',
      confine: true,
      backgroundColor: surface,
      borderColor: line,
      textStyle: { color: ink, fontFamily: 'inherit', fontSize: 13 },
      formatter: (item: { name: string; value: number }) =>
        `${item.name}: <b>${item.value}</b> ${t('admin.dashboard.ordersUnit')}`,
    },
    xAxis: {
      type: 'category',
      data: props.days.map((day) => day.label),
      axisTick: { show: false },
      axisLine: { lineStyle: { color: line } },
      axisLabel: { color: muted, fontSize: 11, hideOverlap: true },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      axisLabel: { color: muted, fontSize: 11 },
      splitLine: { lineStyle: { color: line, width: 1, type: 'solid' } },
    },
    series: [
      {
        type: 'bar',
        barMaxWidth: 24,
        data: props.days.map((day) => day.count),
        itemStyle: { color: bar, borderRadius: [4, 4, 0, 0] },
        emphasis: { itemStyle: { color: bar, opacity: 0.85 } },
      },
    ],
  })
}

onMounted(() => {
  if (!host.value) {
    return
  }

  chart = init(host.value)
  render()
  observer = new ResizeObserver(() => chart?.resize())
  observer.observe(host.value)
})

watch(() => [props.days, theme.isDark.value], render)

onBeforeUnmount(() => {
  observer?.disconnect()
  chart?.dispose()
})
</script>

<template>
  <div
    ref="host"
    class="h-64 w-full"
    role="img"
    :aria-label="t('admin.dashboard.chartTitle')"
    data-testid="orders-chart"
  />
</template>
