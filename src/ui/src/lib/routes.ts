// Bảng trang của khung Luyện tập (nhúng trong app WPF): tên trong #hash → component (tải lười).
// Trang ngoài danh sách này (Môn học, Lịch…) do app WPF hiện: App.svelte báo app chuyển trang.

import type { Component } from 'svelte';

type Loader = () => Promise<{ default: Component<{ arg: string }> }>;

// keyArg: trang dựng lại khi tham số đổi (bài khác, đề khác).
export const PAGES: Record<string, { load: Loader; keyArg?: boolean }> = {
	'luyen-tap': { load: () => import('../pages/Practice.svelte') },
	bai: { load: () => import('../pages/Lesson.svelte'), keyArg: true },
	'on-cau-sai': { load: () => import('../pages/Review.svelte'), keyArg: true },
	'on-hom-nay': { load: () => import('../pages/TodayReview.svelte'), keyArg: true },
	'danh-dau': { load: () => import('../pages/Flagged.svelte') },
	thi: { load: () => import('../pages/Exam.svelte'), keyArg: true },
	'ket-qua': { load: () => import('../pages/Results.svelte') },
	trang: { load: () => import('../pages/StaticPage.svelte'), keyArg: true },
	// Không keyArg: đổi tab (Tạo/Nhập/Xuất/AI) giữ nguyên chữ đang gõ.
	soan: { load: () => import('../pages/Soan.svelte') },
	'kho-quiz': { load: () => import('../pages/QuizArchive.svelte') },
};
