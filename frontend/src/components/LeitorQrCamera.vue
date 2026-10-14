<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { cameraDisponivel, criarDetectorQr } from '../qrcode'

/**
 * Câmera ao vivo lendo QR code (celular, notebook ou webcam). O navegador pede permissão na primeira vez.
 * Só funciona em conexão segura (https ou localhost); fora disso, avisa e a tela usa a foto.
 */
const emit = defineEmits<{ lido: [texto: string]; fechar: [] }>()
const video = ref<HTMLVideoElement | null>(null)
const mensagem = ref('')
const cameras = ref<MediaDeviceInfo[]>([])
const atual = ref(0)
const resolucao = ref('')
let stream: MediaStream | null = null
let parar = false

async function abrir(deviceId?: string) {
  fechar()
  parar = false
  mensagem.value = ''
  try {
    stream = await navigator.mediaDevices.getUserMedia({
      // Resolução alta: o QR da nota é pequeno e denso; em 640×480 (o padrão de muitas webcams) quase não dá para ler.
      video: {
        ...(deviceId ? { deviceId: { exact: deviceId } } : { facingMode: { ideal: 'environment' } }),
        width: { ideal: 1920 },
        height: { ideal: 1080 },
      },
      audio: false,
    })
    video.value!.srcObject = stream
    await video.value!.play()
    resolucao.value = `${video.value!.videoWidth}×${video.value!.videoHeight}`
    // Depois da permissão os nomes das câmeras aparecem: dá para trocar (frontal, traseira, webcam).
    cameras.value = (await navigator.mediaDevices.enumerateDevices()).filter((d) => d.kind === 'videoinput')
    const usada = stream.getVideoTracks()[0]?.getSettings().deviceId
    atual.value = Math.max(0, cameras.value.findIndex((c) => c.deviceId === usada))
    procurar()
  } catch (e) {
    const nome = (e as DOMException).name
    mensagem.value = nome === 'NotAllowedError'
      ? 'Permissão da câmera negada. Libere no cadeado ao lado do endereço do site e tente de novo.'
      : nome === 'NotFoundError' ? 'Nenhuma câmera encontrada neste aparelho.'
        : 'Não consegui abrir a câmera (ela pode estar em uso por outro programa).'
  }
}

async function procurar() {
  const detectar = await criarDetectorQr()
  const passo = async () => {
    if (parar || !video.value) return
    if (video.value.readyState >= 2) {
      const texto = await detectar(video.value)
      if (texto) {
        navigator.vibrate?.(80)
        fechar()
        emit('lido', texto)
        return
      }
    }
    setTimeout(passo, 180)
  }
  passo()
}

function trocarCamera() {
  if (cameras.value.length < 2) return
  atual.value = (atual.value + 1) % cameras.value.length
  abrir(cameras.value[atual.value].deviceId)
}

function fechar() {
  parar = true
  stream?.getTracks().forEach((t) => t.stop())
  stream = null
}

onMounted(() => {
  if (!cameraDisponivel()) {
    mensagem.value = 'A câmera ao vivo só funciona em conexão segura (https). Neste endereço, use "Foto do QR": abre a câmera do celular do mesmo jeito.'
    return
  }
  abrir()
})
onBeforeUnmount(fechar)
</script>

<template>
  <div class="leitor" data-testid="leitor-qr">
    <v-alert v-if="mensagem" type="warning" variant="tonal" density="compact" class="ma-3">{{ mensagem }}</v-alert>
    <div v-show="!mensagem" class="quadro-video">
      <video ref="video" playsinline muted />
      <div class="mira" />
      <div class="dica text-caption">
        Segure a nota parada com o QR dentro do quadrado (uns 15 a 25 cm).<br>
        Webcam sem foco de perto? Use "Foto do QR" com uma foto tirada no celular. <span style="opacity: 0.6">{{ resolucao }}</span>
      </div>
      <div class="acoes d-flex ga-2">
        <v-btn v-if="cameras.length > 1" icon="mdi-camera-flip-outline" size="small" variant="flat" color="surface" aria-label="Trocar câmera" @click="trocarCamera" />
        <v-btn icon="mdi-close" size="small" variant="flat" color="surface" aria-label="Fechar câmera" @click="fechar(); emit('fechar')" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.quadro-video { position: relative; background: #000; border-radius: 12px; overflow: hidden; margin: 12px; max-height: 60vh; }
video { width: 100%; max-height: 60vh; display: block; object-fit: cover; }
/* Mesma área que o leitor recorta: quadrado no meio com 70% da altura. */
.mira { position: absolute; top: 50%; left: 50%; height: 70%; aspect-ratio: 1; transform: translate(-50%, -50%); border: 3px solid rgba(255, 255, 255, 0.85); border-radius: 16px; box-shadow: 0 0 0 9999px rgba(0, 0, 0, 0.35); }
.dica { position: absolute; left: 0; right: 0; bottom: 10px; text-align: center; color: #fff; }
.acoes { position: absolute; top: 10px; right: 10px; }
</style>
