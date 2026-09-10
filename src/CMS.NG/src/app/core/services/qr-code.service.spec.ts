import { TestBed } from '@angular/core/testing';

import { QrCodeService, safeFileName } from './qr-code.service';

/** Reads the pixel at (x, y) of a canvas as `[r, g, b, a]`. */
function pixelAt(canvas: HTMLCanvasElement, x: number, y: number): number[] {
  const ctx = canvas.getContext('2d');
  return Array.from(ctx!.getImageData(x, y, 1, 1).data);
}

describe('QrCodeService', () => {
  let service: QrCodeService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(QrCodeService);
  });

  it('renders a QR matrix with a caption strip under it', async () => {
    const canvas = await service.render('https://www.uuu.com.tw/Course/Show/35/PLF', 'PLF');

    expect(canvas.width).toBe(512);
    // The matrix is square; the extra height is the caption strip.
    expect(canvas.height).toBeGreaterThan(canvas.width);
  });

  it('draws dark modules — the matrix is not a blank white square', async () => {
    const canvas = await service.render('https://www.uuu.com.tw/Course/Show/35/PLF', 'PLF');
    const ctx = canvas.getContext('2d')!;
    const { data } = ctx.getImageData(0, 0, canvas.width, canvas.width);

    let dark = 0;
    for (let i = 0; i < data.length; i += 4) {
      if (data[i] < 128) {
        dark++;
      }
    }
    // A QR matrix is roughly half dark; anything above a few percent proves it drew.
    expect(dark).toBeGreaterThan(canvas.width * canvas.width * 0.1);
  });

  it('draws the caption inside the strip below the matrix', async () => {
    const withCaption = await service.render('https://www.uuu.com.tw/Course/Show/35/PLF', 'PLF');
    const blank = await service.render('https://www.uuu.com.tw/Course/Show/35/PLF', '');

    const captionRow = (canvas: HTMLCanvasElement) => {
      const ctx = canvas.getContext('2d')!;
      const y = canvas.width + (canvas.height - canvas.width) / 2;
      const { data } = ctx.getImageData(0, Math.round(y), canvas.width, 1);
      let dark = 0;
      for (let i = 0; i < data.length; i += 4) {
        if (data[i] < 128) {
          dark++;
        }
      }
      return dark;
    };

    expect(captionRow(withCaption)).toBeGreaterThan(0);
    expect(captionRow(blank)).toBe(0);
  });

  it('keeps the quiet zone white', async () => {
    const canvas = await service.render('https://www.uuu.com.tw/Course/Show/35/PLF', 'PLF');

    expect(pixelAt(canvas, 2, 2)).toEqual([255, 255, 255, 255]);
  });

  it('encodes the canvas as a PNG blob', async () => {
    const canvas = await service.render('https://www.uuu.com.tw/Course/Show/35/PLF', 'PLF');
    const blob = await service.toPngBlob(canvas);

    expect(blob.type).toBe('image/png');
    expect(blob.size).toBeGreaterThan(0);

    const header = new Uint8Array(await blob.arrayBuffer()).subarray(0, 8);
    // PNG magic bytes.
    expect(Array.from(header)).toEqual([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
  });

  it('saves a blob through a download link and releases the object URL', () => {
    const anchor = document.createElement('a');
    spyOn(anchor, 'click');
    spyOn(document, 'createElement').and.returnValue(anchor);
    spyOn(URL, 'createObjectURL').and.returnValue('blob:test');
    const revoke = spyOn(URL, 'revokeObjectURL');

    service.save(new Blob(['x'], { type: 'image/png' }), 'PLF.png');

    expect(anchor.click).toHaveBeenCalled();
    expect(anchor.download).toBe('PLF.png');
    expect(anchor.getAttribute('href')).toBe('blob:test');
    expect(revoke).toHaveBeenCalledWith('blob:test');
    expect(anchor.isConnected).toBeFalse();
  });

  describe('safeFileName', () => {
    it('replaces characters Windows refuses and trims the edges', () => {
      expect(safeFileName('AI/ML:Basics.png')).toBe('AI-ML-Basics.png');
      expect(safeFileName(' 23aiNFA .png ')).toBe('23aiNFA .png');
    });

    it('keeps spaces, parentheses and Chinese — live CourseIds carry all three', () => {
      expect(safeFileName('DO180(NO).png')).toBe('DO180(NO).png');
      expect(safeFileName('Python-程式設計開發必修.png')).toBe('Python-程式設計開發必修.png');
    });

    it('falls back to a name when everything is stripped', () => {
      expect(safeFileName('   ')).toBe('qrcode.png');
    });
  });
});
