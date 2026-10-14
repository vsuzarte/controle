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
