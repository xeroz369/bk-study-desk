// Gói ví dụ cho test. Ví dụ lấy từ đề thi thật (hcmut-*) chỉ có ở bản private, repo public chỉ có vi-du.md:
// đọc bằng import.meta.glob nên thiếu file thì test liên quan được bỏ qua, không làm hỏng build.
const files = import.meta.glob('../../../../../studypack/examples/*', { query: '?raw', import: 'default', eager: true }) as Record<
	string,
	string
>;

/** Nội dung file ví dụ theo tên, không có (bản public) thì undefined. */
export const example = (name: string): string | undefined => Object.entries(files).find(([path]) => path.endsWith('/' + name))?.[1];

/** Nội dung file ví dụ chắc chắn có (vi-du.md có ở cả hai bản). */
export const required = (name: string): string => {
	const t = example(name);
	if (t === undefined) throw new Error(`Thiếu studypack/examples/${name}`);
	return t;
};
