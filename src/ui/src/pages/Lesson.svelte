<!-- Một bài học: kiến thức (HTML + TeX trong content/) rồi câu luyện tập. -->
<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import Tag from '$lib/components/app/Tag.svelte';
	import QuestionCard from '$lib/components/app/QuestionCard.svelte';
	import SourceList from '$lib/components/app/SourceList.svelte';
	import { Button } from '$lib/components/ui/button';
	import ArrowLeft from '@lucide/svelte/icons/arrow-left';
	import ArrowRight from '@lucide/svelte/icons/arrow-right';
	import { typeset } from '$lib/math';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';
	import { STATUS_TEXT } from '$lib/study/types';
	import { scopeOfLesson, scopePath } from '$lib/study/transfer';
	import ShuffleToggles from '$lib/components/app/ShuffleToggles.svelte';
	import { loadPrefs, optionOrder, rng, seedFor, shuffled } from '$lib/study/shuffle';

	let { arg }: { arg: string } = $props();
	const entry = $derived(Study.entry(arg));
	const lesson = $derived(Study.lessons[arg]);
	const list = $derived(Study.entries());
	const i = $derived(list.findIndex((x) => x.id === arg));
	const prev = $derived(list[i - 1]);
	const next = $derived(list[i + 1]);
	const status = $derived(Progress.status(arg));

	// Shuffle: seed fixed while the page is open so the order does not jump on re-render.
	const seed = Date.now();
	// Default from the pack settings until the learner picks their own (then remembered on this device).
	const initial = untrack(() => Study.lessons[arg]?.shuffle); // page is rebuilt per lesson (keyArg)
	let prefs = $state(loadPrefs({ questions: !!initial?.questions, options: !!initial?.options }));
	const shown = $derived(prefs.questions ? shuffled(lesson?.questions ?? [], rng(seed)) : (lesson?.questions ?? []));
	const orders = $derived(
		Object.fromEntries(shown.map((q) => [q.id, prefs.options ? optionOrder(q, rng(seedFor(seed, q.id))) : undefined])),
	);

	onMount(() => {
		if (Study.lessons[arg]) Progress.openLesson(arg);
	});
</script>

{#if !entry}
	<PageShell title="Không tìm thấy bài" crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]} />
{:else}
	<PageShell title={entry.title} crumbs={[{ label: entry.courseName, href: '#luyen-tap/' + entry.courseId }]} meta={entry.unitTitle}>
		{#snippet actions()}
			{@const sc = scopeOfLesson(arg)}
			{#if sc}<Button size="xs" variant="ghost" href={`#soan/tao/${scopePath(sc)}`}>Soạn · Nhập · Xuất</Button>{/if}
			<Tag tone={status === 'xong' ? 'ok' : status === 'dang-hoc' ? 'warn' : 'muted'} text={STATUS_TEXT[status]} />
		{/snippet}
	</PageShell>

	<div class="flex flex-col gap-stack">
		<SourceList sources={lesson?.sources ?? entry.sources} subject={entry.courseName} />
		{#if !lesson}
			<Panel pad><p class="text-sm text-muted-foreground">Bài này chưa có câu hỏi. Bấm Soạn · Nhập · Xuất ở trên để thêm.</p></Panel>
		{:else}
			<Panel title="Kiến thức" pad>
				<div class="prose-lesson" use:typeset={arg}>
					{#each lesson.sections ?? [] as s, k (k)}
						{#if s.title}<h3>{@html s.title}</h3>{/if}
						{@html s.html}
					{/each}
				</div>
			</Panel>
			{#if lesson.questions?.length}
				<Panel title="Luyện tập" meta="Sai thì chọn lại" pad>
					{#snippet action()}<ShuffleToggles bind:prefs />{/snippet}
					{#each shown as q, k (q.id)}
						<QuestionCard
							question={q}
							index={k}
							mode="practice"
							order={orders[q.id]}
							onanswer={(q, j, ok) => Progress.recordAnswer(q.id, j, ok)}
						/>
					{/each}
				</Panel>
			{/if}
		{/if}
		<div class="flex justify-between gap-2">
			{#if prev}<Button size="sm" variant="outline" href={'#bai/' + prev.id}><ArrowLeft />{prev.title}</Button>{:else}<span></span>{/if}
			{#if next}<Button size="sm" variant="outline" href={'#bai/' + next.id}>{next.title}<ArrowRight /></Button>{/if}
		</div>
	</div>
{/if}
