<!-- Kho quiz LMS (#kho-quiz): quizzes you finished on LMS, saved after each sync (switch: sources.lms.saveQuizzes,
     same value as the box in Settings). Review, similar questions by AI, share once the quiz has closed. -->
<script lang="ts">
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import DataRow from '$lib/components/app/DataRow.svelte';
	import Tag from '$lib/components/app/Tag.svelte';
	import { Button } from '$lib/components/ui/button';
	import * as Alert from '$lib/components/ui/alert';
	import { Switch } from '$lib/components/ui/switch';
	import { onMount } from 'svelte';
	import { api } from '$lib/api/client';
	import { Study } from '$lib/study/registry.svelte';
	import { UNIT_TITLE, type QuizInfo } from '$lib/study/lmsquiz';
	import { scopeOfLesson, scopePath } from '$lib/study/transfer';

	let { arg: _arg }: { arg: string } = $props(); // every page gets arg from the router
	let note = $state('');
	let autoSave = $state(true);
	onMount(async () => {
		autoSave = (await api.prefs().catch(() => null))?.saveQuizzes ?? true;
	});
	async function setAutoSave(on: boolean) {
		autoSave = on;
		const r = await api.setPrefs({ saveQuizzes: on }).catch(() => null);
		autoSave = r?.saveQuizzes ?? !on;
		note = r
			? on
				? 'Đã bật: quiz nộp xong sẽ được lưu ở lần đồng bộ LMS tới.'
				: 'Đã tắt: app không lưu quiz mới. Quiz đã lưu vẫn giữ.'
			: 'Không đổi được cài đặt.';
	}
	const groups = $derived.by(() => {
		const m = new Map<string, QuizInfo[]>();
		for (const i of [...Study.quizInfo].reverse()) m.set(i.quiz.subject, [...(m.get(i.quiz.subject) ?? []), i]);
		return [...m];
	});
	const day = (t?: number) => (t ? new Date(t * 1000).toLocaleDateString('vi-VN') : '');

	function status(i: QuizInfo): { tone: 'ok' | 'warn' | 'muted'; text: string } {
		if (i.quiz.noReview) return { tone: 'muted', text: 'không cho xem lại' };
		if (!i.usable) return { tone: 'warn', text: 'chưa có đáp án' };
		if (i.unknown) return { tone: 'warn', text: `${i.unknown} câu chưa rõ đáp án` };
		return { tone: 'ok', text: 'đủ đáp án' };
	}
	function sub(i: QuizInfo) {
		const q = i.quiz;
		return [
			q.finished ? `làm ngày ${day(q.finished)}` : '',
			q.grade != null ? `điểm ${q.grade}` : '',
			i.usable ? `${i.usable} câu ôn được` : '',
			q.answers === false && !q.noReview && q.closesAt ? `đáp án có sau ${day(q.closesAt)}` : '',
			i.lessonId && !i.shareable ? (q.closesAt ? `chia sẻ được sau ${day(q.closesAt)}` : 'chia sẻ được khi quiz đóng') : '',
		]
			.filter(Boolean)
			.join(' · ');
	}
	/** Quiz with no review: write down what you remember, as a lesson in "Quiz LMS đã lưu" (only on this computer, no LMS
	 *  call). You check the answers yourself. */
	function recall(i: QuizInfo) {
		const course = Study.manifest.courses.find((c) => c.code?.toUpperCase() === (i.quiz.code ?? '').toUpperCase());
		if (!course) {
			note = 'Chưa có môn này trong app. Đồng bộ LMS trước.';
			return;
		}
		const ui = course.units.findIndex((u) => u.title === UNIT_TITLE);
		sessionStorage.setItem('soan.prefill', JSON.stringify({ unit: UNIT_TITLE, lesson: `Ghi lại: ${i.quiz.quiz}` }));
		location.hash = `#soan/tao/${course.id}${ui >= 0 ? '/' + ui : ''}`;
	}
	function menu(i: QuizInfo) {
		const sc = i.lessonId ? scopeOfLesson(i.lessonId) : null;
		if (!i.lessonId || !sc) return [{ label: 'Ghi lại câu còn nhớ', primary: true, run: () => recall(i) }];
		return [
			{ label: 'Ôn lại', primary: true, run: () => (location.hash = '#bai/' + i.lessonId) },
			{ label: 'Câu tương tự (nhờ AI)', run: () => (location.hash = `#soan/ai/${scopePath(sc)}`) },
			...(i.shareable ? [{ label: 'Xuất để chia sẻ', run: () => (location.hash = `#soan/xuat/${scopePath(sc)}`) }] : []),
		];
	}
	async function saveNow() {
		const r = await api.syncLms();
		note = r
			? 'Đang đồng bộ LMS. Quiz vừa nộp sẽ vào kho sau khoảng một phút.'
			: 'Chưa lưu được lúc này: app đang hoặc vừa đồng bộ, hoặc chưa đăng nhập.';
	}
</script>

<PageShell title="Kho quiz LMS" crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]} meta="quiz bạn đã nộp trên LMS">
	{#snippet actions()}
		<label class="flex min-h-6 items-center gap-2 text-xs" title="Giống ô trong Cài đặt → Đồng bộ và nhắc hạn">
			<Switch checked={autoSave} onCheckedChange={setAutoSave} aria-label="Tự lưu quiz đã nộp" />Tự lưu sau khi nộp
		</label>
		<Button size="xs" variant="ghost" onclick={saveNow} disabled={!autoSave}>Lưu ngay</Button>
	{/snippet}
</PageShell>

<div class="flex flex-col gap-stack">
	{#if note}<Alert.Root><Alert.Title>{note}</Alert.Title></Alert.Root>{/if}
	<Panel title="Cách hoạt động" pad collapsible open={!Study.quizInfo.length}>
		<ul class="list-disc space-y-1 pl-5 text-sm">
			<li>Mỗi lần đồng bộ, app lưu bản xem lại của quiz <b>đã nộp</b>. Không đọc quiz đang làm.</li>
			<li>Quiz ẩn đáp án thì app tự lấy lại sau giờ đóng.</li>
			<li>Quiz không cho xem lại: chỉ lưu tên và điểm; chọn <i>Ghi lại</i> để tự ghi câu còn nhớ.</li>
			<li>Chia sẻ được sau khi quiz đóng.</li>
		</ul>
	</Panel>
	{#each groups as [subject, list] (subject)}
		<Panel title={subject} meta={`${list.length} quiz`}>
			{#each list as i (i.quiz.file)}
				{@const st = status(i)}
				<DataRow title={i.quiz.quiz} sub={sub(i)} menu={() => menu(i)}>
					{#snippet trailing()}
						<Tag tone={st.tone} text={st.text} />
						{#if i.lessonId}<Button size="xs" variant="outline" href={'#bai/' + i.lessonId}>Ôn lại</Button>
						{:else}<Button size="xs" variant="outline" title="Tự ghi lại câu còn nhớ để làm lại; đáp án tự kiểm" onclick={() => recall(i)}
								>Ghi lại</Button
							>{/if}
					{/snippet}
				</DataRow>
			{/each}
		</Panel>
	{:else}
		<Panel pad
			><p class="text-sm text-muted-foreground">
				{autoSave
					? 'Chưa có quiz nào. Nộp quiz trên LMS rồi chọn Lưu ngay, hoặc chờ lần đồng bộ sau.'
					: 'Tự lưu đang tắt. Bật công tắc ở trên để lưu quiz đã nộp.'}
			</p></Panel
		>
	{/each}
</div>
