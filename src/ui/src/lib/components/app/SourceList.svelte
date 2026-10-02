<!-- Nguồn của bài/đề: tên file · trang. Có tên môn thì bấm để mở file đúng trang (thư mục Môn học\<môn>). -->
<script lang="ts">
	import type { SourceRef } from '$lib/study/types';
	import { api } from '$lib/api/client';
	import BookMarked from '@lucide/svelte/icons/book-marked';

	let { sources = [], subject }: { sources?: SourceRef[]; subject?: string } = $props();
	let missing = $state<Record<number, boolean>>({});

	/** "20–37" -> 20; "câu 8–11" -> 0 (not a page) */
	function firstPage(pages?: string) {
		const m = /^\s*(\d+)/.exec(pages ?? '');
		return m ? Number(m[1]) : 0;
	}
	async function open(k: number, s: SourceRef) {
		missing[k] = !(await api.openSource(subject!, s.file, firstPage(s.pages)).catch(() => null));
	}
</script>

{#if sources.length}
	<span class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
		<BookMarked class="size-3.5" />
		{#each sources as s, k (k)}
			{@const label = `${s.file}${s.pages ? ` · ${/^\d/.test(s.pages) ? 'tr.' : ''}${s.pages}` : ''}`}
			{#if subject}
				<button
					class="link inline-flex min-h-6 items-center"
					title="Mở file{firstPage(s.pages) ? ' ở trang ' + firstPage(s.pages) : ''}"
					onclick={() => open(k, s)}>{label}</button
				>
				{#if missing[k]}<span class="text-bad">không thấy file trong Môn học\{subject}</span>{/if}
			{:else}
				<span>{label}</span>
			{/if}
		{/each}
	</span>
{/if}
