// Đề ngẫu nhiên và xáo câu có nhóm. Hàm thuần (không đụng registry, không DOM) để test được.
// Nhóm (question.group): các câu cùng nhóm trong một bài dùng chung đề bài hay số liệu ("Câu 3-5 dùng hàm f..."),
// nên luôn được rút cùng nhau, đứng liền nhau và giữ thứ tự gốc.

import type { Blueprint, Question } from './types';

/** Nhãn chung cho nút, menu và tiêu đề đề ngẫu nhiên. */
export const RANDOM_EXAM = 'Đề ngẫu nhiên';

/** Khóa nhóm của một câu trong bài; câu không có nhóm thì đứng riêng. */
const groupKey = (q: Question, lesson: number | string) => (q.group ? `${lesson}|${q.packId ?? ''}|${q.group}` : undefined);

/** Gom câu thành khối: câu cùng nhóm vào một khối (ở vị trí câu đầu tiên), câu lẻ mỗi câu một khối. */
export function blocks<T extends Question>(qs: T[], lessonOf: (q: T) => number | string = (q) => q.lessonId ?? q.examId ?? ''): T[][] {
	const out: T[][] = [];
	const byKey = new Map<string, T[]>();
	for (const q of qs) {
		const k = groupKey(q, lessonOf(q));
		const have = k ? byKey.get(k) : undefined;
		if (have) have.push(q);
		else {
			const b = [q];
			if (k) byKey.set(k, b);
			out.push(b);
		}
	}
	return out;
}

/** Xáo thứ tự các khối, giữ nguyên thứ tự câu trong mỗi khối (câu cùng nhóm luôn liền nhau). */
export function shuffleGroups<T extends Question>(qs: T[], rand: () => number): T[] {
	const b = blocks(qs);
	for (let i = b.length - 1; i > 0; i--) {
		const j = Math.floor(rand() * (i + 1));
		[b[i], b[j]] = [b[j], b[i]];
	}
	return b.flat();
}

/** Đề ngẫu nhiên có kiểu môn (course.blueprint) có thì theo, không thì `count` câu trải theo số câu từng chương. */
export interface RandomPick {
	/** câu đã rút, theo thứ tự gốc (chương, bài, câu) */
	questions: Question[];
	/** số câu dự tính (theo đề thật) để co thời gian khi rút được ít hơn */
	total: number;
}

/**
 * Rút câu cho đề ngẫu nhiên từ câu trong bài, mỗi fingerprint một lần.
 * `units[chương][bài]` là danh sách câu. `rank`: nhỏ hơn thì được rút trước (vd. chưa làm, tới hạn ôn); bằng nhau thì ngẫu nhiên.
 * Chương thiếu câu thì nhường phần còn lại cho chương khác (chỉ chương có trong đề thật nếu có blueprint.units).
 */
export function pickRandomExam(
	units: Question[][][],
	bp: Blueprint | undefined,
	count: number,
	rank: (q: Question) => number = () => 0,
	random: () => number = Math.random,
): RandomPick {
	const seen = new Set<string>();
	const pos = new Map<Question, number[]>();
	const pools = units.map((lessons, ui) => {
		const items = lessons.flatMap((qs, li) => {
			// Câu trùng (cùng fingerprint) chỉ giữ lần đầu; câu trùng trong một nhóm thì nhóm bớt câu đó.
			const fresh = qs.filter((q, qi) => {
				if (!q.fp || seen.has(q.fp)) return false;
				seen.add(q.fp);
				pos.set(q, [ui, li, qi]);
				return true;
			});
			return blocks(fresh, () => li).map((b) => ({ qs: b, k: Math.min(...b.map(rank)) + random() }));
		});
		return items.sort((a, b) => a.k - b.k);
	});
	const sizes = pools.map((p) => p.reduce((n, it) => n + it.qs.length, 0));
	const total = bp?.units ? bp.units.reduce((a, b) => a + b, 0) : (bp?.count ?? count);
	let want = bp?.units ? sizes.map((n, i) => Math.min(bp.units![i] ?? 0, n)) : spread(total, sizes);
	const short = total - want.reduce((a, b) => a + b, 0);
	if (short > 0) {
		// Bù chỉ từ chương mà đề thật có ra (blueprint > 0), không lấy từ gói hay quiz LMS đã lưu.
		const extra = spread(
			short,
			sizes.map((n, i) => (!bp?.units || (bp.units[i] ?? 0) > 0 ? n - want[i] : 0)),
		);
		want = want.map((n, i) => n + extra[i]);
	}
	// Khối lớn hơn chỗ còn trống thì bỏ qua, lấy khối sau: đề có thể ít hơn dự tính một vài câu nhưng không cắt đôi nhóm.
	const picked = pools.flatMap((items, i) => {
		const take: Question[] = [];
		for (const it of items) if (take.length + it.qs.length <= want[i]) take.push(...it.qs);
		return take;
	});
	const at = (q: Question) => pos.get(q)!;
	picked.sort((a, b) => {
		const x = at(a),
			y = at(b);
		return x[0] - y[0] || x[1] - y[1] || x[2] - y[2];
	});
	return { questions: picked, total };
}

/** Chia `total` cho các nhóm theo cỡ (largest remainder), không nhóm nào nhận quá số nó có. */
export function spread(total: number, sizes: number[]): number[] {
	const sum = sizes.reduce((a, b) => a + b, 0);
	if (!sum) return sizes.map(() => 0);
	const raw = sizes.map((n) => (Math.min(total, sum) * n) / sum);
	const out = raw.map(Math.floor);
	let rest = Math.min(total, sum) - out.reduce((a, b) => a + b, 0);
	const order = raw.map((r, i) => [r - Math.floor(r), i]).sort((a, b) => b[0] - a[0]);
	for (const [, i] of order) if (rest > 0 && out[i] < sizes[i]) (out[i]++, rest--);
	return out;
}

/** Đủ câu để ra đề ngẫu nhiên: 10 câu trở lên, hoặc môn có kiểu đề thật và có ít nhất một câu. */
export const enoughForRandomExam = (questions: number, hasBlueprint: boolean) => questions >= 10 || (hasBlueprint && questions > 0);
