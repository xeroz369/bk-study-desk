// Fingerprint là khóa của lịch ôn, ghi chú, thời gian làm câu trong ket-qua.json: đổi giá trị là mất tiến độ đã lưu.
// Các giá trị dưới đây chụp từ bản 1.1.6, chỉ được sửa khi cố ý đổi cách tính (kèm migrate dữ liệu).
import { describe, expect, it } from 'vitest';
import { fingerprint, fpText } from './fingerprint';
import type { StudyPack } from './pack';
import { example } from './test-examples';

const exampleJson = example('hcmut-mt1009-chia-doi.studypack.json');

const SAMPLES = [
	{ prompt: '<p>Tính \\( \\int_0^1 x\\,dx \\)</p>', options: ['<p>0,5</p>', '1', '2', 'Cả A và B'], answer: 0 },
	{ prompt: 'Đúng hay sai: 1 &lt; 2&nbsp;?', options: ['Đúng', 'Sai'], answer: 0 },
	{ type: 'numeric', prompt: 'Nghiệm của x - 2 = 0', options: [], value: 2, answer: -1 },
	{ type: 'short', prompt: 'Tên phương pháp?', options: [], accept: ['Newton', '  Newton-Raphson '], answer: -1 },
	{ prompt: '<p>Ảnh <img src="data:image/png;base64,AAAA"> ở đây</p>', options: ['A', 'B', 'C'], answer: 2 },
	{ type: 'multi', prompt: 'Chọn các số chẵn', options: ['2', '3', '4'], answers: [0, 2], answer: -1 },
];
const PINNED = ['d3453892e0d90eb9', 'b37705ec71120755', 'dd4e9d0bede23820', 'f78cc7320c99af49', '3457123acc27bccb', 'c96afee3fb683078'];

const EXAMPLE_PINNED = [
	'f3abc540f5f37b4b',
	'b977fed184d6411c',
	'0f510dad8343d3da',
	'4c226cd145af52bc',
	'02bf2f69145bd7a0',
	'ac38ffc90f31e34c',
];

describe('fingerprint', () => {
	it('giữ nguyên giá trị đã lưu', () => {
		expect(SAMPLES.map((q) => fingerprint(q))).toEqual(PINNED);
	});

	it.skipIf(!exampleJson)('giữ nguyên giá trị cho gói ví dụ', () => {
		const p = JSON.parse(exampleJson!) as StudyPack;
		const fps = p.units.flatMap((u) => u.lessons.flatMap((l) => (l.questions ?? []).map((q) => fingerprint(q))));
		expect(fps).toEqual(EXAMPLE_PINNED);
	});

	it('không phụ thuộc thứ tự phương án, khoảng trắng hay thẻ', () => {
		const a = { prompt: '<p>Câu  hỏi</p>', options: ['x', 'y', 'z'] };
		const b = { prompt: 'Câu hỏi', options: ['z', '<b>x</b>', 'y '] };
		expect(fingerprint(a)).toBe(fingerprint(b));
	});

	it('khác đề thì khác fingerprint', () => {
		expect(fingerprint({ prompt: 'a', options: ['1', '2'] })).not.toBe(fingerprint({ prompt: 'b', options: ['1', '2'] }));
	});

	it('fpText bỏ thẻ, entity, khoảng trắng; ảnh thành [img]', () => {
		expect(fpText('<p>A&nbsp;&lt;&amp;&gt; <img src="x"> B</p>')).toBe('a<&>[img]b');
		expect(fpText('\\( a\\,b \\)')).toBe('\\(ab\\)');
	});
});
