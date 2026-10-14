<script setup lang="ts">
import { computed, ref } from 'vue'
import { QrcodeStream, type DetectedBarcode, type EmittedError } from 'vue-qrcode-reader'
import { cameraDisponivel } from '../qrcode'

/**
 * Câmera ao vivo lendo QR code (celular, notebook ou webcam), com o vue-qrcode-reader (ZXing).
 * O navegador pede permissão na primeira vez. Só funciona em conexão segura (https ou localhost).
 */
const emit = defineEmits<{ lido: [texto: string]; fechar: [] }>()
const mensagem = ref(cameraDisponivel() ? '' : 'A câmera ao vivo só funciona em conexão segura (https). Neste endereço, use "Foto do QR": abre a câmera do celular do mesmo jeito.')
const ligada = ref(false)
const cameras = ref<MediaDeviceInfo[]>([])
const atual = ref<string | null>(null)
const temLanterna = ref(false)
const lanterna = ref(false)

/** Câmera traseira por padrão (celular); resolução alta, porque o QR da nota é pequeno e denso. */
const restricoes = computed<MediaTrackConstraints>(() => ({
  ...(atual.value ? { deviceId: { exact: atual.value } } : { facingMode: 'environment' }),
  width: { ideal: 1920 },
  height: { ideal: 1080 },
}))

async function aoLigar(capacidades: Partial<MediaTrackCapabilities> & { torch?: boolean }) {
  ligada.value = true
  temLanterna.value = !!capacidades.torch
  cameras.value = (await navigator.mediaDevices.enumerateDevices()).filter((d) => d.kind === 'videoinput')
}

function aoDetectar(codigos: DetectedBarcode[]) {
  const texto = codigos[0]?.rawValue
  if (!texto) return
  navigator.vibrate?.(80)
  emit('lido', texto)
}

function aoErro(e: EmittedError) {
  const mensagens: Record<string, string> = {
    NotAllowedError: 'Permissão da câmera negada. Libere no cadeado ao lado do endereço do site e tente de novo.',
    NotFoundError: 'Nenhuma câmera encontrada neste aparelho.',
    NotReadableError: 'A câmera está em uso por outro programa (feche videochamadas e tente de novo).',
    OverconstrainedError: 'Esta câmera não aceitou a configuração pedida. Tente trocar de câmera.',
    InsecureContextError: 'A câmera ao vivo só funciona em conexão segura (https). Use "Foto do QR".',
    StreamApiNotSupportedError: 'Este navegador não dá acesso à câmera. Use "Foto do QR".',
  }
  mensagem.value = mensagens[e.name] ?? `Não consegui abrir a câmera (${e.message}).`
}

function trocarCamera() {
  if (cameras.value.length < 2) return
  const i = cameras.value.findIndex((c) => c.deviceId === atual.value)
  atual.value = cameras.value[(i + 1) % cameras.value.length].deviceId
}
</script>

<template>
  <div class="leitor" data-testid="leitor-qr">
    <v-alert v-if="mensagem" type="warning" variant="tonal" density="compact" class="ma-3">{{ mensagem }}</v-alert>
    <div v-else class="quadro-video">
      <QrcodeStream :constraints="restricoes" :formats="['qr_code']" :torch="lanterna" @camera-on="aoLigar" @detect="aoDetectar" @error="aoErro">
        <div v-if="!ligada" class="d-flex justify-center align-center h-100"><v-progress-circular indeterminate color="white" /></div>
        <template v-else>
          <div class="mira" />
          <div class="dica text-caption">Encaixe o QR da nota no quadrado e segure parado.</div>
        </template>
        <div class="acoes d-flex ga-2">
          <v-btn v-if="temLanterna" :icon="lanterna ? 'mdi-flashlight-off' : 'mdi-flashlight'" size="small" variant="flat" color="surface"
            aria-label="Lanterna" @click="lanterna = !lanterna" />
          <v-btn v-if="cameras.length > 1" icon="mdi-camera-flip-outline" size="small" variant="flat" color="surface" aria-label="Trocar câmera" @click="trocarCamera" />
          <v-btn icon="mdi-close" size="small" variant="flat" color="surface" aria-label="Fechar câmera" @click="emit('fechar')" />
        </div>
      </QrcodeStream>
    </div>
  </div>
</template>

<style scoped>
.quadro-video { position: relative; background: #000; border-radius: 12px; overflow: hidden; margin: 12px; height: min(60vh, 480px); }
.mira { position: absolute; top: 50%; left: 50%; height: 70%; aspect-ratio: 1; transform: translate(-50%, -50%); border: 3px solid rgba(255, 255, 255, 0.85); border-radius: 16px; box-shadow: 0 0 0 9999px rgba(0, 0, 0, 0.35); pointer-events: none; }
.dica { position: absolute; left: 0; right: 0; bottom: 10px; text-align: center; color: #fff; }
.acoes { position: absolute; top: 10px; right: 10px; }
</style>
