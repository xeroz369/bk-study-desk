// Luyện trộn: rút câu của nhiều chương rồi xếp xen kẽ, hai câu liền nhau thuộc hai chương khác nhau khi còn chương khác.
// Trộn dạng bài buộc người học tự nhận ra bài nào dùng cách nào; lúc luyện điểm thấp hơn nhưng bài kiểm tra sau cao hơn
// (Rohrer & Taylor 2007). Khác Đề ngẫu nhiên (exam.ts): đề giữ thứ tự chương như đề thật, ở đây cố ý xen kẽ.
// Hàm thuần (không registry, không DOM) để test được.

import { blocks } from './exam';
import type { Question } from './types';

/** Số câu mỗi lượt luyện trộn. */
export const MIX_COUNT = 20;

/** Môn có ít nhất hai chương có câu thì mới trộn được. */
export const canMix = (units: Question[][][]) => units.filter((u) => u.some((qs) => qs.length > 0)).length >= 2;

/**
 * `units[chương][bài]` là danh sách câu. Mỗi fingerprint lấy một lần; câu cùng nhóm đi liền nhau như một khối.
 * Trong từng chương, khối có `rank` nhỏ lên trước (chưa làm, tới hạn ôn), bằng nhau thì ngẫu nhiên.
 * Lấy lần lượt mỗi chương một khối theo vòng; khối lớn hơn chỗ còn trống thì chương đó nhường lượt.
 */
export function interleave(
	units: Question[][][],
	count: number,
	rank: (q: Question) => number = () => 0,
	random: () => number = Math.random,
): Question[] {
	const seen = new Set<string>();
	const pools = units.map((lessons) =>
		lessons
			.flatMap((qs, li) =>
				blocks(
					qs.filter((q) => !!q.fp && !seen.has(q.fp) && !!seen.add(q.fp)),
					() => li,
				),
			)
			.map((b) => ({ b, k: Math.min(...b.map(rank)) + random() }))
			.sort((x, y) => x.k - y.k)
			.map((it) => it.b),
	);
	const out: Question[] = [];
	for (let added = true; added && out.length < count;) {
		added = false;
		for (const pool of pools) {
			const i = pool.findIndex((b) => out.length + b.length <= count);
			if (i < 0) continue;
			out.push(...pool.splice(i, 1)[0]);
			added = true;
		}
	}
	return out;
}
