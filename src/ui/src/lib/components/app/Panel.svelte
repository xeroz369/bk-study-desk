<!-- Khối nội dung gọn: viền mảnh, đầu khối thấp (tiêu đề viết thường kiểu câu, đậm vừa), thân là các hàng chia vạch.
     collapsible: thu gọn được; open chỉ là trạng thái ban đầu (người dùng bấm thì giữ, không bị ghi đè khi dữ liệu làm mới).
     pad: thân có đệm (cho văn bản, lưới) thay vì danh sách hàng. -->
<script lang="ts">
	import { untrack, type Snippet } from 'svelte';
	import * as Collapsible from '$lib/components/ui/collapsible';
	import ChevronRight from '@lucide/svelte/icons/chevron-right';
	import { cn } from '$lib/utils';

	let {
		title = '',
		meta = '',
		collapsible = false,
		open = true,
		pad = false,
		class: className = '',
		head,
		action,
		children,
	}: {
		title?: string;
		meta?: string;
		collapsible?: boolean;
		open?: boolean;
		pad?: boolean;
		class?: string;
		/** nội dung thêm trong đầu khối, sau tiêu đề (thanh tiến độ, nhãn…) */
		head?: Snippet;
		action?: Snippet;
		children: Snippet;
	} = $props();

	let isOpen = $state(untrack(() => open));
	const bodyCls = $derived(pad ? 'p-3' : 'divide-y divide-border/60 px-1 py-0.5');
	// Thân là danh sách hàng: ↑/↓ chuyển giữa các hàng của khối này (lib/desktop.svelte.ts).
</script>

{#snippet header(trigger: boolean)}
	<div class="flex h-panel-head items-center gap-2 px-3 {trigger ? '' : 'border-b'}">
		{#if trigger}<ChevronRight class="size-3.5 shrink-0 text-muted-foreground transition-transform {isOpen ? 'rotate-90' : ''}" />{/if}
		<span class="shrink-0 text-sm font-semibold">{title}</span>
		{@render head?.()}
		{#if meta}<span class="ml-auto shrink-0 text-xs text-muted-foreground tabular-nums">{meta}</span>{/if}
		{#if action}<span class="flex shrink-0 items-center gap-1 {meta ? '' : 'ml-auto'}">{@render action()}</span>{/if}
	</div>
{/snippet}

<section class={cn('min-w-0 overflow-hidden rounded-md border bg-card', className)}>
	{#if collapsible}
		<Collapsible.Root bind:open={isOpen}>
			<Collapsible.Trigger
				class="w-full text-left hover:bg-muted/40 focus-visible:outline-1 focus-visible:-outline-offset-1 focus-visible:outline-ring"
				>{@render header(true)}</Collapsible.Trigger
			>
			<Collapsible.Content
				><div class="border-t {bodyCls}" data-rows={pad ? undefined : true}>{@render children()}</div></Collapsible.Content
			>
		</Collapsible.Root>
	{:else}
		{#if title || action}{@render header(false)}{/if}
		<div class={bodyCls} data-rows={pad ? undefined : true}>{@render children()}</div>
	{/if}
</section>
