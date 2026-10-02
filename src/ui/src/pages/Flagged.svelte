<!-- Đánh dấu: câu có ghi chú riêng hoặc cờ "nghi đáp án sai" (lưu theo fingerprint, nên vẫn còn khi gói được cập nhật).
     "Copy để báo lỗi" chép câu dạng Study Markdown + ghi chú, dán cho người soạn gói hoặc nhờ AI soát lại. -->
<script lang="ts">
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import QuestionCard from '$lib/components/app/QuestionCard.svelte';
	import EmptyRow from '$lib/components/app/EmptyRow.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';
	import { questionMarkdown, quizLocked } from '$lib/study/transfer';
	import type { Question } from '$lib/study/types';

	let { arg: _arg }: { arg: string } = $props();
	const all = $derived([...Study.lessonQuestions(), ...Object.values(Study.exams).flatMap((x) => x.questions ?? [])]);
	// One card per fingerprint; flagged first, newest note first.
	const items = $derived.by(() => {
		const byFp = new Map<string, Question>();
		const active = new Set(Progress.activeNotes().map(([fp]) => fp));
		for (const q of all) if (q.fp && active.has(q.fp) && !byFp.has(q.fp)) byFp.set(q.fp, q);
		return [...byFp.values()].sort((a, b) => {
			const na = Progress.state.notes[a.fp!],
				nb = Progress.state.notes[b.fp!];
			return Number(!!nb.flag) - Number(!!na.flag) || nb.at.localeCompare(na.at);
		});
	});
	const where = (q: Question) => {
		const e = Study.entry(q.lessonId ?? '');
		if (e) return `${e.courseName} · ${e.title}`;
		const x = q.examId ? Study.exams[q.examId] : undefined;
		return x ? `${Study.course(x.courseId)?.name ?? ''} · ${x.title}` : '';
	};
	let copied = $state('');
	async function copy(qs: Question[], key: string) {
		const text = qs
			.map((q) => {
				const n = Progress.state.notes[q.fp!];
				const head = [`<!-- ${where(q)}${q.packId ? ` · gói ${q.packId}` : ''} -->`];
				if (n?.flag) head.push('<!-- nghi đáp án hoặc lời giải sai -->');
				if (n?.text) head.push(`<!-- ghi chú: ${n.text.replace(/-->/g, '—>')} -->`);
				return [...head, questionMarkdown(q)].join('\n');
			})
			.join('\n\n');
		await navigator.clipboard.writeText(text);
		copied = key;
		setTimeout(() => (copied = ''), 2000);
	}
	const flagged = $derived(items.filter((q) => Progress.state.notes[q.fp!]?.flag));
	// A saved LMS quiz that is still open is not copied out.
	const copyable = $derived(flagged.filter((q) => !quizLocked(q.lessonId ?? '')));
</script>

<PageShell
	title="Đánh dấu"
	crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]}
	meta={`${flagged.length} câu nghi sai · ${items.length - flagged.length} câu có ghi chú`}
>
	{#snippet actions()}
		{#if copyable.length}
			<Button size="xs" variant="ghost" onclick={() => copy(copyable, 'all')}
				>{copied === 'all' ? 'Đã sao chép' : `Sao chép ${copyable.length} câu nghi sai để báo lỗi`}</Button
			>
		{/if}
	{/snippet}
</PageShell>

<Panel pad>
	{#each items as q, k (q.fp)}
		<div class="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
			<span>{where(q)}</span>
			{#if !quizLocked(q.lessonId ?? '')}<Button size="xs" variant="ghost" class="ml-auto" onclick={() => copy([q], q.fp!)}
					>{copied === q.fp ? 'Đã sao chép' : 'Sao chép để báo lỗi'}</Button
				>{/if}
		</div>
		<QuestionCard question={q} index={k} mode="practice" onanswer={(q, j, ok) => Progress.recordAnswer(q.id, j, ok)} />
	{:else}
		<EmptyRow>Chưa có câu nào. Câu bạn ghi chú hoặc đánh dấu nghi sai sẽ hiện ở đây.</EmptyRow>
	{/each}
</Panel>
