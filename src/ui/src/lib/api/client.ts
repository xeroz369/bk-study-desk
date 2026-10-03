// Lời gọi tới app (C#) từ khung Luyện tập. Mọi yêu cầu đi qua https://sohoc.example/api/* — không có cổng mạng (Api/ApiRouter.cs).

import type { ProgressState } from '$lib/study/types';
import type { StudyPack } from '$lib/study/pack';
import type { SavedQuiz } from '$lib/study/lmsquiz';

/** App sends this header on every /api call; the C# side refuses calls without it (a foreign page cannot add it without a CORS preflight). */
const APP = { 'X-App': '1' };

/** Lỗi từ /api: message là chữ `error` server trả về (ApiResponse.Error), status để phân biệt 404 với lỗi khác. */
export class ApiError extends Error {
	constructor(
		readonly status: number,
		message: string,
	) {
		super(message);
		this.name = 'ApiError';
	}
}

/** Đọc `{error}` của response lỗi; body không phải JSON thì dùng mã HTTP. */
async function fail(r: Response): Promise<never> {
	let text = '';
	try {
		text = ((await r.json()) as { error?: unknown }).error?.toString() ?? '';
	} catch {
		/* body rỗng hoặc không phải JSON */
	}
	throw new ApiError(r.status, text || `HTTP ${r.status}`);
}

async function get<T>(path: string): Promise<T> {
	const r = await fetch('api/' + path, { cache: 'no-store', headers: APP });
	if (!r.ok) await fail(r);
	return r.json() as Promise<T>;
}

async function send<T = unknown>(method: 'POST' | 'PUT', path: string, body?: unknown): Promise<T> {
	const r = await fetch('api/' + path, {
		method,
		headers: { ...APP, 'Content-Type': 'application/json' },
		body: body === undefined ? undefined : JSON.stringify(body),
	});
	if (!r.ok) await fail(r);
	return (await r.json()) as T;
}

/** Chữ của một lỗi bất kỳ (Error, ApiError, chuỗi) để hiện cho người dùng. */
export const errorText = (e: unknown) => (e instanceof Error ? e.message : String(e));

const q = encodeURIComponent;

export const api = {
	state: () => get<ProgressState>('state'),
	saveState: (s: ProgressState) => send('PUT', 'state', s),
	/** nguồn của bài (tên môn + file như trong content/), mở đúng trang; lỗi 404 = không tìm thấy file */
	openSource: (subject: string, file: string, page = 0) => send('POST', `source?subject=${q(subject)}&file=${q(file)}&page=${page}`),
	/** web trường mở trong cửa sổ của app (dùng chung đăng nhập), trang khác ra trình duyệt */
	openWeb: (url: string, title = '') => send('POST', `app/open-web?url=${q(url)}&title=${q(title)}`),
	/** môn đang học kỳ này (LMS + đăng ký MyBK): chọn môn khi soạn */
	subjects: () => get<{ code: string; name: string }[]>('subjects'),
	/** quiz LMS đã làm, đã lưu bản xem lại (data/lms-quiz) */
	quizzes: () => get<SavedQuiz[]>('quizzes'),
	/** công tắc tự lưu quiz LMS (cùng giá trị với Cài đặt) */
	prefs: () => get<{ saveQuizzes: boolean }>('prefs'),
	setPrefs: (p: { saveQuizzes: boolean }) => send<{ saveQuizzes: boolean }>('POST', `prefs?saveQuizzes=${p.saveQuizzes}`),
	/** đồng bộ LMS ngay (lưu quiz vừa nộp) */
	syncLms: () => send('POST', 'lms/sync'),
	/** gói luyện tập đã cài (content/packs/*.studypack.json) */
	packs: () => get<{ file: string; size: number }[]>('packs'),
	importPack: (pack: StudyPack) => send<{ ok: boolean; file: string }>('POST', 'packs', pack),
	deletePack: (id: string) => send('POST', `packs/delete?id=${q(id)}`),
};
