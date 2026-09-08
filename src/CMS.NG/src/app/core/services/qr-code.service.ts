import { Injectable } from '@angular/core';
import { toCanvas } from 'qrcode';

/** Side of the QR matrix in the composed image, in pixels. */
const MATRIX_SIZE = 512;
/** Height of the caption strip drawn under the matrix. */
const CAPTION_HEIGHT = 72;
const CAPTION_FONT = '600 34px "Segoe UI", "Microsoft JhengHei", system-ui, sans-serif';
/** Left/right breathing room the caption is squeezed into. */
const CAPTION_PADDING = 24;
/** Characters Windows and macOS refuse in a file name. */
const UNSAFE_FILENAME_CHARS = /[\/:*?"<>|]/g;

@Injectable({ providedIn: 'root' })
export class QrCodeService {
  /**
   * Renders `text` as a QR matrix with `caption` drawn underneath it, on one canvas.
   * The page displays this canvas and the download saves it, so what a user sees on
   * screen and what lands in the PNG are the same image.
   */
  async render(text: string, caption: string): Promise<HTMLCanvasElement> {
    const matrix = document.createElement('canvas');
    await toCanvas(matrix, text, {
      width: MATRIX_SIZE,
      margin: 2,
      errorCorrectionLevel: 'M',
      color: { dark: '#000000ff', light: '#ffffffff' },
    });

    const canvas = document.createElement('canvas');
    canvas.width = matrix.width;
    canvas.height = matrix.height + CAPTION_HEIGHT;

    const ctx = canvas.getContext('2d');
    if (!ctx) {
      throw new Error('無法取得 canvas 繪圖環境。');
    }

    // The matrix carries its own quiet zone; the strip below it only needs a white ground.
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(matrix, 0, 0);

    ctx.fillStyle = '#000000';
    ctx.font = CAPTION_FONT;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    // maxWidth squeezes a long CourseId (18 chars live, some with spaces) instead of
    // letting it run off the edge.
    ctx.fillText(
      caption,
      canvas.width / 2,
      matrix.height + CAPTION_HEIGHT / 2,
      canvas.width - CAPTION_PADDING * 2,
    );

    return canvas;
  }

  /** Encodes a rendered canvas as a PNG blob. */
  toPngBlob(canvas: HTMLCanvasElement): Promise<Blob> {
    return new Promise((resolve, reject) => {
      canvas.toBlob(
        (blob) => (blob ? resolve(blob) : reject(new Error('無法產生 PNG 圖檔。'))),
        'image/png',
      );
    });
  }

  /** Hands `blob` to the browser as a download; `filename` is stripped of illegal characters. */
  save(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = safeFileName(filename);
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }
}

/**
 * CourseId is free text — 15 live values carry spaces, parentheses or Chinese, and a
 * trailing space alone makes a file name Windows will not write.
 */
export function safeFileName(name: string): string {
  return name.replace(UNSAFE_FILENAME_CHARS, '-').trim() || 'qrcode.png';
}
