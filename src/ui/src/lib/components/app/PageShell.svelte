<!-- Thanh công cụ của trang (cố định trên vùng nội dung, không cuộn theo). Trang khai báo PageShell ở đầu; phần đầu
     này được chuyển lên ô #toolbar của khung app (App.svelte), nên vùng nội dung bên dưới tự cuộn như ứng dụng Windows.
     Hàng 1: đường dẫn/tiêu đề · mô tả · nút.
     Hàng 2 (nếu có tab): tab gạch chân, cuộn ngang khi hẹp. tools: hàng phụ (bộ lọc…). -->
<script lang="ts">
	import type { Snippet } from 'svelte';
	import * as Tabs from '$lib/components/ui/tabs';

	let {
		title,
		crumbs = [],
		tabs = [],
		tab = '',
		base = '',
		meta = '',
		actions,
		tools,
		children,
	}: {
		title: string;
		crumbs?: { label: string; href: string }[];
		tabs?: [key: string, label: string, count?: number][];
		tab?: string;
		base?: string;
		meta?: string;
		actions?: Snippet;
		tools?: Snippet;
		children?: Snippet;
	} = $props();

	/** Chuyển phần tử lên ô thanh công cụ của khung app; trả lại chỗ cũ khi trang đóng. */
	function toToolbar(node: HTMLElement) {
		const slot = document.getElementById('toolbar');
		slot?.replaceChildren(node);
		return () => node.remove();
	}
</script>

<!-- Giữ chỗ: chỉ phần tử bên trong được chuyển đi, để Svelte vẫn gỡ đúng nút của trang khi đổi trang. -->
<div hidden>
	<header class="bg-background px-3" {@attach toToolbar}>
		<div class="flex h-10 items-center gap-2">
			<nav class="flex min-w-0 flex-1 items-center gap-1" aria-label="Đường dẫn">
				{#each crumbs as c (c.href)}
					<a
						href={c.href}
						draggable="false"
						class="inline-flex h-7 max-w-48 shrink-0 items-center truncate rounded-sm px-1.5 text-sm text-muted-foreground hover:bg-muted hover:text-foreground"
						title={c.label}>{c.label}</a
					>
					<span class="text-muted-foreground" aria-hidden="true">›</span>
				{/each}
				<h1 class="min-w-0 shrink truncate px-0.5 text-base font-semibold" {title}>{title}</h1>
				{#if meta}<span class="ml-1.5 min-w-0 shrink-[4] truncate text-xs text-muted-foreground" title={meta}>{meta}</span>{/if}
			</nav>
			<div class="flex shrink-0 items-center gap-1.5">
				{@render actions?.()}
			</div>
		</div>
		{#if tabs.length}
			<Tabs.Root value={tab} onValueChange={(v) => (location.hash = '#' + base + (v ? '/' + v : ''))} class="-mt-1 overflow-x-auto">
				<Tabs.List variant="line" class="h-8 gap-0">
					{#each tabs as [key, label, count] (key)}
						<Tabs.Trigger value={key} class="h-8 px-2.5 text-sm text-muted-foreground data-[state=active]:text-foreground">
							{label}{#if count}<span class="ml-1 text-xs text-muted-foreground tabular-nums" aria-label={count + ' chưa đọc'}
									>({count})</span
								>{/if}
						</Tabs.Trigger>
					{/each}
				</Tabs.List>
			</Tabs.Root>
		{/if}
		{#if tools}<div class="flex flex-wrap items-center gap-2 pt-1 pb-2">{@render tools()}</div>{/if}
		{@render children?.()}
	</header>
</div>
