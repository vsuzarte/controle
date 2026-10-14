import jsQR from 'jsqr'

/**
 * Lê o QR code de uma foto (câmera do celular ou imagem salva). Funciona sem HTTPS, ao contrário da câmera ao vivo.
 * Usa o leitor do próprio navegador quando existe (Android/Chrome) e o jsQR nos demais.
 */
export async function lerQrDaImagem(arquivo: File): Promise<string | null> {
  const bitmap = await createImageBitmap(arquivo)
  try {
    const Detector = (window as unknown as { BarcodeDetector?: new (o: { formats: string[] }) => { detect(i: ImageBitmap): Promise<{ rawValue: string }[]> } }).BarcodeDetector
    if (Detector) {
      const achados = await new Detector({ formats: ['qr_code'] }).detect(bitmap).catch(() => [])
      if (achados[0]?.rawValue) return achados[0].rawValue
    }
    // Foto grande deixa a leitura lenta: reduz para no máximo 1600px no maior lado.
    const escala = Math.min(1, 1600 / Math.max(bitmap.width, bitmap.height))
    const canvas = document.createElement('canvas')
    canvas.width = Math.round(bitmap.width * escala)
    canvas.height = Math.round(bitmap.height * escala)
    const ctx = canvas.getContext('2d', { willReadFrequently: true })!
    ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
    const img = ctx.getImageData(0, 0, canvas.width, canvas.height)
    return jsQR(img.data, img.width, img.height, { inversionAttempts: 'attemptBoth' })?.data ?? null
  } finally {
    bitmap.close()
  }
}

/** Câmera ao vivo só existe em conexão segura (https ou localhost); em http pela rede o navegador nem oferece. */
export function cameraDisponivel() {
  return window.isSecureContext && !!navigator.mediaDevices?.getUserMedia
}

/** Leitor para usar quadro a quadro no vídeo da câmera: o do navegador quando existe, senão o jsQR. */
export async function criarDetectorQr(): Promise<(video: HTMLVideoElement) => Promise<string | null>> {
  type Detector = { detect(i: CanvasImageSource): Promise<{ rawValue: string }[]> }
  const Nativo = (window as unknown as { BarcodeDetector?: new (o: { formats: string[] }) => Detector }).BarcodeDetector
  if (Nativo) {
    const detector = new Nativo({ formats: ['qr_code'] })
    return async (video) => (await detector.detect(video).catch(() => []))[0]?.rawValue ?? null
  }
  const canvas = document.createElement('canvas')
  const ctx = canvas.getContext('2d', { willReadFrequently: true })!
  return async (video) => {
    const escala = Math.min(1, 960 / Math.max(video.videoWidth, video.videoHeight))
    canvas.width = Math.round(video.videoWidth * escala)
    canvas.height = Math.round(video.videoHeight * escala)
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height)
    const img = ctx.getImageData(0, 0, canvas.width, canvas.height)
    return jsQR(img.data, img.width, img.height, { inversionAttempts: 'dontInvert' })?.data ?? null
  }
}
