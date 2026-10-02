<!-- Một menu chuột phải cho cả vùng nội dung. Bấm phải vào hàng có menu (lib/menu.svelte.ts) thì hiện lệnh của hàng đó;
     chỗ khác không có menu. Ô nhập và chữ đang chọn để WebView2 hiện Cắt/Sao chép/Dán (Shell/MainForm.cs lọc bớt). -->
<script lang="ts">
	import type { Snippet } from 'svelte';
	import * as ContextMenu from '$lib/components/ui/context-menu';
	import { menuFor, type MenuItem } from '$lib/menu.svelte';

	let { children }: { children: Snippet<[Record<string, unknown>]> } = $props();
	let items = $state<MenuItem[]>([]);
	let open = $state(false);

	// Bắt ở pha capture, trước ContextMenu: không có lệnh thì chặn để menu không mở.
	function capture(e: MouseEvent) {
		const editing = (e.target as Element | null)?.closest('input, textarea, [contenteditable]');
		const selected = !!window.getSelection()?.toString();
		const hit = menuFor(e.target);
		if (!hit || editing || selected) {
			e.stopPropagation();
			if (!editing && !selected) e.preventDefault();
			return;
		}
		items = hit.items;
		hit.el.closest<HTMLElement>('[data-row]')?.focus({ preventScroll: true });
	}
	$effect(() => {
		document.addEventListener('contextmenu', capture, true);
		return () => document.removeEventListener('contextmenu', capture, true);
	});
</script>

<ContextMenu.Root bind:open>
	<ContextMenu.Trigger>
		{#snippet child({ props })}{@render children(props)}{/snippet}
	</ContextMenu.Trigger>
	<ContextMenu.Content class="min-w-48">
		{#each items as it, i (i)}
			{#if it.sep && i > 0}<ContextMenu.Separator />{/if}
			<ContextMenu.Item disabled={it.disabled} class={it.primary ? 'font-semibold' : ''} onSelect={() => it.run?.()}>
				{it.label}
				{#if it.hint}<ContextMenu.Shortcut>{it.hint}</ContextMenu.Shortcut>{/if}
			</ContextMenu.Item>
		{/each}
	</ContextMenu.Content>
</ContextMenu.Root>
