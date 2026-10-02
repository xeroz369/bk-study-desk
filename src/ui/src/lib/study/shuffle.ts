// Shuffle questions and options for practice and mock exams.
// Answers are always stored by ORIGINAL option index, so grading and saved results never change.

import { plainText } from './text';
import type { Question } from './types';

/** Options that refer to other options must keep their place ("Cả A và B", "Các đáp án khác đều sai"…). */
const POSITIONAL =
	/^\s*(tất cả|tat ca|các (đáp án|phương án|câu)|cac (dap an|phuong an)|cả\s+\S+\s+(và|lẫn)|ca\s+\S+\s+va|không (có )?(đáp án|phương án|câu) nào|none of|all of|both\b)/i;

/** Small seeded RNG so the order stays the same while the page is open (re-render safe). */
export function rng(seed: number) {
	let s = seed >>> 0 || 1;
	return () => (s = (s * 1664525 + 1013904223) >>> 0) / 2 ** 32;
}

/** Seed for one question: same page seed + question id → same option order even if questions are re-shuffled. */
export function seedFor(seed: number, id: string) {
	let h = seed >>> 0;
	for (let i = 0; i < id.length; i++) h = Math.imul(h ^ id.charCodeAt(i), 16777619) >>> 0;
	return h;
}

export function shuffled<T>(items: T[], rand: () => number): T[] {
	const a = [...items];
	for (let i = a.length - 1; i > 0; i--) {
		const j = Math.floor(rand() * (i + 1));
		[a[i], a[j]] = [a[j], a[i]];
	}
	return a;
}

/** Display order of a question's options (display position -> original index). Identity when not allowed. */
export function optionOrder(q: Question, rand: () => number): number[] {
	const idx = q.options.map((_, i) => i);
	if (q.keepOrder || q.options.length < 3) return idx;
	const text = (i: number) => plainText(q.options[i]);
	const free = idx.filter((i) => !POSITIONAL.test(text(i)));
	const mixed = shuffled(free, rand);
	let k = 0;
	return idx.map((i) => (POSITIONAL.test(text(i)) ? i : mixed[k++]));
}

/** Remembered toggles (per viewer, this device). */
export interface ShufflePrefs {
	questions: boolean;
	options: boolean;
}
export function loadPrefs(fallback: ShufflePrefs): ShufflePrefs {
	try {
		const v = JSON.parse(localStorage.getItem('practice.shuffle') ?? 'null');
		return v && typeof v.questions === 'boolean' ? v : fallback;
	} catch {
		return fallback;
	}
}
export function savePrefs(p: ShufflePrefs) {
	try {
		localStorage.setItem('practice.shuffle', JSON.stringify(p));
	} catch {
		/* storage blocked: keep in memory only */
	}
}
