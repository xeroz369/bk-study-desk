// Nội dung học (content/*.js) và kết quả luyện tập (data/ket-qua.json). Cấu trúc: DESIGN.md.

export interface SourceRef {
	file: string;
	pages?: string;
}
/** Loại câu (giống quiz Moodle). Đúng/Sai được đổi thành single ["Đúng", "Sai"] lúc nạp. */
export type QuestionType = 'single' | 'multi' | 'numeric' | 'short';
/** Câu trả lời: chỉ số (single), mảng chỉ số (multi), chuỗi người dùng gõ (numeric, short). */
export type Answer = number | number[] | string;

export interface Question {
	id: string;
	type?: QuestionType;
	tag?: string;
	prompt: string; // HTML, có TeX \( \) \[ \]
	options: string[];
	answer: number;
	/** multi: các chỉ số đúng */
	answers?: number[];
	/** numeric: đáp số, sai số tuyệt đối cho phép, đơn vị */
	value?: number;
	tolerance?: number;
	unit?: string;
	/** short: các đáp án chữ được chấp nhận */
	accept?: string[];
	solution: string;
	lessonId?: string;
	examId?: string;
	/** không xáo phương án (có phương án kiểu "Cả A và B") */
	keepOrder?: boolean;
	/** nhóm câu dùng chung đề, số liệu: đề ngẫu nhiên rút cả nhóm, xáo câu giữ nhóm liền nhau (exam.ts) */
	group?: string;
	/** Câu đến từ gói luyện tập nào (để hiện nguồn, xóa câu tự soạn). */
	packId?: string;
	/** fingerprint (fingerprint.ts): same question, same fp, wherever it comes from */
	fp?: string;
}
export interface Lesson {
	id: string;
	title: string;
	sources?: SourceRef[];
	sections?: { title?: string; html: string }[];
	questions?: Question[];
	/** default shuffle (from the pack settings); the learner can still toggle it */
	shuffle?: { questions?: boolean; options?: boolean };
}
export interface Exam {
	id: string;
	courseId: string;
	title: string;
	minutes: number;
	sources?: SourceRef[];
	questionIds?: string[];
	questions?: Question[];
	scoring?: Scoring;
	shuffle?: { questions?: boolean; options?: boolean };
}
export interface Page {
	id: string;
	html: string;
}
export interface Scoring {
	count: number;
	right: number;
	wrong: number;
}
export interface LessonEntry {
	id: string;
	title: string;
	file?: string;
	sources?: SourceRef[];
}
export interface PackInfo {
	id: string;
	title: string;
	authors: string;
}
export interface Unit {
	title: string;
	lessons: LessonEntry[];
	/** Chương đến từ gói luyện tập (content/packs), không phải content/*.js. */
	pack?: PackInfo;
}
export interface Course {
	id: string;
	name: string;
	/** Mã môn (MT1009…): gói luyện tập cùng mã sẽ ghép vào môn này. */
	code?: string;
	exam?: { date: string; time: string };
	scoring?: Scoring;
	/** Real exam layout for random mock exams: questions per chapter (by index), minutes, scoring. */
	blueprint?: Blueprint;
	units: Unit[];
}
export interface Blueprint {
	minutes: number;
	/** questions per unit index; missing = spread over all chapters by size */
	units?: number[];
	/** total when `units` is not given (default: scoring.count) */
	count?: number;
	scoring?: Scoring;
}
export interface Manifest {
	term: string;
	priority?: string[];
	pages?: { id: string; title: string; file: string }[];
	exams?: { file: string }[];
	courses: Course[];
}
export interface Entry extends LessonEntry {
	courseId: string;
	courseName: string;
	unitTitle: string;
}

export interface QuestionRecord {
	tries: number;
	firstTry: boolean;
	correct: boolean;
	lastCorrect: boolean;
	reviewedOk: boolean;
	chosen: Answer;
	at: string;
}
export interface ExamAttempt {
	examId: string;
	startedAt: string;
	seconds: number;
	answers: Record<string, Answer>;
	right: number;
	wrong: number;
	blank: number;
	score: number;
	max: number;
	/** title kept for generated exams (random mock exams are not in the book after a restart) */
	title?: string;
	/** seconds spent per question id */
	times?: Record<string, number>;
}
/** Spaced repetition (Leitner) state of one question, keyed by fingerprint. */
export interface SrsRecord {
	box: number;
	/** next review day, YYYY-MM-DD (local) */
	due: string;
	/** last day it was answered */
	last: string;
	seen: number;
	wrong: number;
}
export interface NoteRecord {
	text?: string;
	/** "nghi đáp án sai" */
	flag?: boolean;
	at: string;
}
export interface ProgressState {
	version: 1;
	updatedAt: string | null;
	questions: Record<string, QuestionRecord>;
	lessons: Record<string, { openedAt: string }>;
	exams: ExamAttempt[];
	srs: Record<string, SrsRecord>;
	notes: Record<string, NoteRecord>;
	/** last seconds spent on a question (by fingerprint) */
	times: Record<string, number>;
}

export type LessonStatus = 'chua-soan' | 'chua-hoc' | 'dang-hoc' | 'xong';
export const STATUS_TEXT: Record<LessonStatus, string> = {
	'chua-soan': 'Chưa có câu',
	'chua-hoc': 'Chưa học',
	'dang-hoc': 'Đang học',
	xong: 'Xong',
};
