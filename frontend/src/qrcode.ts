import { BarcodeDetector, setZXingModuleOverrides } from 'barcode-detector/pure'
import zxingWasm from 'zxing-wasm/reader/zxing_reader.wasm?url'

/*
 * Leitura de QR code com o ZXing (o mesmo leitor de muitos apps de celular), compilado em WebAssembly.
 * Usado pela câmera ao vivo (vue-qrcode-reader, que compartilha este mesmo módulo) e pela foto.
 * O arquivo .wasm é servido pelo próprio sistema: não depende de site externo.
 */
setZXingModuleOverrides({
  locateFile: (caminho: string, prefixo: string) => (caminho.endsWith('.wasm') ? zxingWasm : prefixo + caminho),
})

/** Câmera ao vivo só existe em conexão segura (https ou localhost); em http pela rede o navegador nem oferece. */
export function cameraDisponivel() {
  return window.isSecureContext && !!navigator.mediaDevices?.getUserMedia
}

/** Lê o QR code de uma foto (câmera do celular ou imagem salva). Funciona sem HTTPS. */
export async function lerQrDaImagem(arquivo: File): Promise<string | null> {
  const detector = new BarcodeDetector({ formats: ['qr_code'] })
  const achados = await detector.detect(arquivo)
  return achados[0]?.rawValue ?? null
}
