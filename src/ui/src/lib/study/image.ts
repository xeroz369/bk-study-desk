// Shrink an image picked or pasted while editing: max 1200 px on the long side, WebP ~85%.
// Keeps packs light (usually 30–100 KB per image) and avoids huge screenshots.

const MAX_SIDE = 1200;

export async function compressImage(file: Blob): Promise<string> {
	const bmp = await createImageBitmap(file);
	const scale = Math.min(1, MAX_SIDE / Math.max(bmp.width, bmp.height));
	const canvas = document.createElement('canvas');
	canvas.width = Math.round(bmp.width * scale);
	canvas.height = Math.round(bmp.height * scale);
	const ctx = canvas.getContext('2d')!;
	ctx.fillStyle = '#fff'; // transparent screenshots read better on white in both themes
	ctx.fillRect(0, 0, canvas.width, canvas.height);
	ctx.drawImage(bmp, 0, 0, canvas.width, canvas.height);
	bmp.close();
	const webp = canvas.toDataURL('image/webp', 0.85);
	return webp.startsWith('data:image/webp') ? webp : canvas.toDataURL('image/jpeg', 0.85);
}

/** First image in a paste/drop event, if any. */
export function imageFrom(dt: DataTransfer | null): File | null {
	for (const item of dt?.items ?? []) if (item.kind === 'file' && item.type.startsWith('image/')) return item.getAsFile();
	return null;
}
