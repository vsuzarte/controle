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

/**
 * Leitor para usar quadro a quadro no vídeo da câmera. O QR da nota é pequeno e denso (um link longo), então:
 * lê o recorte do meio da imagem (onde fica a mira) na resolução original, sem reduzir; a cada poucos quadros
 * tenta também a imagem inteira e com cores invertidas. O leitor do navegador (Android/Mac) é usado quando suporta QR.
 */
export async function criarDetectorQr(): Promise<(video: HTMLVideoElement) => Promise<string | null>> {
  type Detector = { detect(i: CanvasImageSource): Promise<{ rawValue: string }[]> }
  type ClasseDetector = { new (o: { formats: string[] }): Detector; getSupportedFormats?: () => Promise<string[]> }
  const Nativo = (window as unknown as { BarcodeDetector?: ClasseDetector }).BarcodeDetector
  const nativo = Nativo && (await Nativo.getSupportedFormats?.().catch((): string[] => []))?.includes('qr_code')
    ? new Nativo({ formats: ['qr_code'] }) : null

  const canvas = document.createElement('canvas')
  const ctx = canvas.getContext('2d', { willReadFrequently: true })!
  let quadro = 0
  const ler = (sx: number, sy: number, lado: number, larguraAlvo: number, alturaAlvo: number, video: HTMLVideoElement, inverter: boolean) => {
    canvas.width = larguraAlvo
    canvas.height = alturaAlvo
    ctx.drawImage(video, sx, sy, lado, lado * (alturaAlvo / larguraAlvo), 0, 0, larguraAlvo, alturaAlvo)
    const img = ctx.getImageData(0, 0, larguraAlvo, alturaAlvo)
    return jsQR(img.data, img.width, img.height, { inversionAttempts: inverter ? 'attemptBoth' : 'dontInvert' })?.data ?? null
  }

  return async (video) => {
    if (nativo) {
      const achado = (await nativo.detect(video).catch(() => []))[0]?.rawValue
      if (achado) return achado
    }
    const w = video.videoWidth
    const h = video.videoHeight
    if (!w || !h) return null
    quadro++
    // Recorte quadrado do meio (70% do lado menor) na resolução original. Testado: reduzir a imagem inteira só lia QR
    // de 130px+; o recorte lê a partir de ~100px. Ampliar o recorte piora (borra os módulos).
    const lado = Math.round(Math.min(w, h) * 0.7)
    const alvo = Math.min(lado, 1200)
    const meio = ler(Math.round((w - lado) / 2), Math.round((h - lado) / 2), lado, alvo, alvo, video, quadro % 3 === 0)
    if (meio || quadro % 2) return meio
    // Quadro sim, quadro não: a imagem inteira (QR fora da mira).
    const escala = Math.min(1, 1280 / Math.max(w, h))
    return ler(0, 0, w, Math.round(w * escala), Math.round(h * escala), video, false)
  }
}
