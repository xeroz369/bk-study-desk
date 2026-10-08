<!-- Kết quả luyện tập: tiến độ từng môn và lịch sử thi thử (data/ket-qua.json). -->
<script lang="ts">
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import EmptyRow from '$lib/components/app/EmptyRow.svelte';
	import * as Table from '$lib/components/ui/table';
	import { clock, dateIso, num } from '$lib/format';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';

	let { arg: _arg }: { arg: string } = $props();
	const attempts = $derived(Progress.state.exams.slice().reverse());
</script>

<PageShell
	title="Kết quả"
	crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]}
	meta={Progress.state.updatedAt ? 'cập nhật lúc ' + dateIso(Progress.state.updatedAt) : ''}
/>

<div class="flex flex-col gap-stack">
	<Panel title="Tiến độ từng môn">
		<Table.Root class="text-sm">
			<Table.Header
				><Table.Row class="[&_th]:h-7 [&_th]:text-xs"
					><Table.Head>Môn</Table.Head><Table.Head class="text-right">Bài xong</Table.Head><Table.Head class="text-right"
						>Câu đã làm</Table.Head
					><Table.Head class="text-right">Đúng lần đầu</Table.Head></Table.Row
				></Table.Header
			>
			<Table.Body>
				{#each Study.manifest.courses as c (c.id)}
					{@const s = Progress.courseStats(c.id)}
					<Table.Row class="[&_td]:py-1">
						<Table.Cell><a class="link inline-flex min-h-6 items-center" href={'#luyen-tap/' + c.id}>{c.name}</a></Table.Cell>
						<Table.Cell class="text-right tabular-nums">{s.done}/{s.total}</Table.Cell>
						<Table.Cell class="text-right tabular-nums">{s.answered}</Table.Cell>
						<Table.Cell class="text-right tabular-nums"
							>{s.answered ? Math.round((s.firstOk / s.answered) * 100) + '%' : '–'}</Table.Cell
						>
					</Table.Row>
				{/each}
			</Table.Body>
		</Table.Root>
	</Panel>

	<Panel title="Lịch sử thi thử" meta={attempts.length ? attempts.length + ' lần' : ''}>
		{#if attempts.length}
			<Table.Root class="text-sm">
				<Table.Header
					><Table.Row class="[&_th]:h-7 [&_th]:text-xs"
						><Table.Head>Lúc</Table.Head><Table.Head>Đề</Table.Head><Table.Head class="text-right">Điểm</Table.Head><Table.Head
							class="text-right">Đúng/Sai/Trống</Table.Head
						><Table.Head class="text-right">Thời gian</Table.Head></Table.Row
					></Table.Header
				>
				<Table.Body>
					{#each attempts as a (a.examId + a.startedAt)}
						<Table.Row class="[&_td]:py-1">
							<Table.Cell class="tabular-nums">{dateIso(a.startedAt)}</Table.Cell>
							<Table.Cell>{Study.exams[a.examId]?.title ?? a.title ?? a.examId}</Table.Cell>
							<Table.Cell class="text-right tabular-nums">{num(a.score)}/{num(a.max)}</Table.Cell>
							<Table.Cell class="text-right tabular-nums">{a.right}/{a.wrong}/{a.blank}</Table.Cell>
							<Table.Cell class="text-right tabular-nums">{clock(a.seconds)}</Table.Cell>
						</Table.Row>
					{/each}
				</Table.Body>
			</Table.Root>
		{:else}<EmptyRow>Chưa làm đề nào.</EmptyRow>{/if}
	</Panel>
</div>
