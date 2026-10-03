import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError, errorText } from './client';

const reply = (status: number, body: unknown) =>
	vi.fn(async () => new Response(typeof body === 'string' ? body : JSON.stringify(body), { status }));

afterEach(() => vi.unstubAllGlobals());

describe('api client', () => {
	it('response lỗi ném ApiError mang chữ {error} của server', async () => {
		vi.stubGlobal('fetch', reply(400, { error: 'id gói không hợp lệ' }));
		const e = await api.deletePack('X').catch((x: unknown) => x);
		expect(e).toBeInstanceOf(ApiError);
		expect((e as ApiError).status).toBe(400);
		expect(errorText(e)).toBe('id gói không hợp lệ');
	});

	it('GET lỗi cũng ném, body không phải JSON thì dùng mã HTTP', async () => {
		vi.stubGlobal('fetch', reply(500, 'oops'));
		await expect(api.packs()).rejects.toThrow('HTTP 500');
	});

	it('response OK trả JSON', async () => {
		vi.stubGlobal('fetch', reply(200, { ok: true, file: 'a.studypack.json' }));
		await expect(api.importPack({} as never)).resolves.toEqual({ ok: true, file: 'a.studypack.json' });
	});
});
