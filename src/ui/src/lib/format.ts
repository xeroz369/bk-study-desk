// Định dạng ngày giờ, số, kích thước. Thời gian nguồn trường là giây Unix.

const p2 = (n: number) => String(n).padStart(2, '0');
const DAY_SHORT = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
const DAY_LONG = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];

export const nowSec = () => Date.now() / 1000;

/** T3 15/10 09:00 */
export function dateSec(sec?: number | null) {
	if (!sec) return '';
	const d = new Date(sec * 1000);
	return `${DAY_SHORT[d.getDay()]} ${p2(d.getDate())}/${p2(d.getMonth() + 1)} ${p2(d.getHours())}:${p2(d.getMinutes())}`;
}

/** 15/10 09:00 từ chuỗi ISO */
export function dateIso(iso?: string | null) {
	if (!iso) return '';
	const d = new Date(iso);
	return `${p2(d.getDate())}/${p2(d.getMonth() + 1)} ${p2(d.getHours())}:${p2(d.getMinutes())}`;
}

/** Thứ ba 15/10 */
export const dateLong = (d: Date) => `${DAY_LONG[d.getDay()]} ${p2(d.getDate())}/${p2(d.getMonth() + 1)}`;

export const hm = (sec: number) => {
	const d = new Date(sec * 1000);
	return `${p2(d.getHours())}:${p2(d.getMinutes())}`;
};

/** Số ngày lịch từ hôm nay: hôm nay = 0, ngày mai = 1. */
export function dayDiff(sec: number) {
	const a = new Date();
	a.setHours(0, 0, 0, 0);
	const b = new Date(sec * 1000);
	b.setHours(0, 0, 0, 0);
	return Math.round((b.getTime() - a.getTime()) / 864e5);
}

/** còn 3 giờ · ngày mai · còn 5 ngày · đã qua */
export function until(sec: number, now = nowSec()) {
	const s = sec - now;
	if (s < 0) return 'đã qua';
	if (s < 3600) return `còn ${Math.max(1, Math.round(s / 60))} phút`;
	const d = dayDiff(sec);
	if (d === 0) return `còn ${Math.round(s / 3600)} giờ`;
	return d === 1 ? 'ngày mai' : `còn ${d} ngày`;
}

/** 2 giờ trước · hôm qua · 3 ngày trước · 12/09 */
export function ago(sec: number, now = Date.now() / 1000) {
	const s = now - sec;
	if (s < 3600) return `${Math.max(1, Math.round(s / 60))} phút trước`;
	const d = -dayDiff(sec);
	if (d === 0) return `${Math.round(s / 3600)} giờ trước`;
	if (d === 1) return 'hôm qua';
	if (d < 7) return `${d} ngày trước`;
	const t = new Date(sec * 1000);
	return `${p2(t.getDate())}/${p2(t.getMonth() + 1)}`;
}

/** "13:00" → 780 (phút), để sắp giờ đúng thứ tự số (chuỗi "13:00" < "7:00"). */
export const minutes = (hhmm: string) => {
	const [h, m] = hhmm.split(':').map(Number);
	return (h || 0) * 60 + (m || 0);
};

/** 0,63 — số kiểu Việt Nam */
export const num = (x: number, digits = 2) => (Math.round(x * 1000) / 1000).toFixed(digits).replace('.', ',');

export const clock = (sec: number) => {
	const s = Math.max(0, Math.round(sec));
	return `${p2(Math.floor(s / 60))}:${p2(s % 60)}`;
};

export function size(b: number) {
	if (b < 1024) return `${b} B`;
	if (b < 1048576) return `${Math.round(b / 1024)} KB`;
	return `${(b / 1048576).toFixed(1).replace('.', ',')} MB`;
}

/** Tuần ISO (MyBK đánh số tuần theo năm dương lịch). */
export function isoWeek(d: Date) {
	const t = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
	const day = t.getUTCDay() || 7;
	t.setUTCDate(t.getUTCDate() + 4 - day);
	return Math.ceil(((t.getTime() - Date.UTC(t.getUTCFullYear(), 0, 1)) / 864e5 + 1) / 7);
}

/** Thứ theo MyBK: 2 = thứ hai … 8 = chủ nhật. */
export const mybkDay = (d: Date) => (d.getDay() === 0 ? 8 : d.getDay() + 1);
export const MYBK_DAYS: Record<number, string> = {
	2: 'Thứ hai',
	3: 'Thứ ba',
	4: 'Thứ tư',
	5: 'Thứ năm',
	6: 'Thứ sáu',
	7: 'Thứ bảy',
	8: 'Chủ nhật',
};

/** "2026-10-15" + "09g00" → giây */
export function examTime(date: string, time: string) {
	const d = new Date(date + 'T00:00:00');
	const m = time.match(/(\d+)g(\d+)/);
	if (m) d.setHours(+m[1], +m[2]);
	return d.getTime() / 1000;
}

export const FILE_KIND: Record<string, string> = {
	'.pdf': 'PDF',
	'.doc': 'Word',
	'.docx': 'Word',
	'.ppt': 'Slide',
	'.pptx': 'Slide',
	'.xls': 'Excel',
	'.xlsx': 'Excel',
	'.png': 'Ảnh',
	'.jpg': 'Ảnh',
	'.jpeg': 'Ảnh',
	'.mp4': 'Video',
	'.m': 'MATLAB',
	'.py': 'Python',
	'.c': 'C',
	'.txt': 'Văn bản',
	'.zip': 'Nén',
	'.rar': 'Nén',
	'.7z': 'Nén',
};
export const fileKind = (ext: string) => FILE_KIND[ext.toLowerCase()] ?? (ext ? ext.slice(1).toUpperCase() : 'File');

/** Nhóm theo ngày kiểu File Explorer (Hôm nay, Ngày mai, …) cho bảng xếp theo thời gian. */
export function dayGroup(sec: number) {
	const d = dayDiff(sec);
	return d < 0 ? 'Đã qua' : d === 0 ? 'Hôm nay' : d === 1 ? 'Ngày mai' : d <= 7 ? 'Trong 7 ngày' : d <= 30 ? 'Trong 30 ngày' : 'Sau đó';
}
