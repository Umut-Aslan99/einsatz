/** Downscales a camera photo before it goes into the outbox: ~150 KB instead of ~5 MB, still sharp on A4. */
export async function compressImage(file: File, maxSide = 1600, quality = 0.8): Promise<{ base64: string; contentType: string }> {
  const bitmap = await createImageBitmap(file);
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(bitmap.width * scale);
  canvas.height = Math.round(bitmap.height * scale);
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  const dataUrl = canvas.toDataURL('image/jpeg', quality);
  return { base64: dataUrl.split(',')[1]!, contentType: 'image/jpeg' };
}
