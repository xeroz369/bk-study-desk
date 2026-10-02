<!-- Hàng dữ liệu mật độ cao, nền của mọi danh sách: [đầu] tiêu đề · phụ ……… meta [cuối].
     href → liên kết trong app; onclick → hành động; không có → chỉ hiển thị. Dựng trên Item của shadcn.
     Kiểu Windows: con trỏ mũi tên, hàng đang chọn tô nền, ↑/↓ chuyển hàng (data-row), chuột phải ra menu
     (menu riêng, hoặc mặc định: Mở · Sao chép tên). -->
<script lang="ts">
	import type { Snippet } from 'svelte';
	import * as Item from '$lib/components/ui/item';
	import { copy, menu as menuOf, type MenuItem } from '$lib/menu.svelte';
	import { cn } from '$lib/utils';

	let {
		title,
		sub = '',
		meta = '',
		href,
		onclick,
		menu,
		dim = false,
		strong = false,
		stack = false,
		metaClass = '',
		class: className = '',
		leading,
		trailing,
		children,
	}: {
		title: string;
		sub?: string;
		meta?: string;
		href?: string;
		onclick?: () => void;
		/** lệnh chuột phải; lệnh primary là việc của bấm/Enter */
		menu?: () => MenuItem[];
		dim?: boolean;
		/** chữ đậm cho tiêu đề (ví dụ thông báo chưa đọc) */
		strong?: boolean;
		/** dòng phụ xuống hàng dưới thay vì nằm cùng dòng */
		stack?: boolean;
		metaClass?: string;
		class?: string;
		leading?: Snippet;
		trailing?: Snippet;
		children?: Snippet;
	} = $props();

	const active = $derived(!!href || !!onclick);
	const cls = $derived(
		cn(
			'flex-nowrap items-center gap-2.5 rounded-sm border-0 px-row py-row text-left',
			active &&
				'hover:bg-muted/60 focus:bg-accent focus-visible:ring-0 focus-visible:outline-1 focus-visible:-outline-offset-1 focus-visible:outline-ring',
			dim && 'opacity-55',
			className,
		),
	);
	const items = (): MenuItem[] => {
		if (menu) return menu();
		const open: MenuItem[] = href
			? [{ label: 'Mở', primary: true, run: () => (location.hash = href) }]
			: onclick
				? [{ label: 'Mở', primary: true, run: onclick }]
				: [];
		return [...open, { label: 'Sao chép tên', sep: open.length > 0, run: () => void copy(title) }];
	};
</script>

{#snippet body()}
	{#if leading}<span class="flex shrink-0 items-center gap-2">{@render leading()}</span>{/if}
	<!-- Tiêu đề được ưu tiên chỗ: dòng phụ co lại trước (shrink-[4]). -->
	<Item.Content class={cn('min-w-0 gap-0', stack ? 'flex-col' : 'flex-row items-baseline')}>
		<Item.Title class={cn('block max-w-full min-w-0 shrink truncate', strong ? 'font-semibold' : 'font-normal')} {title}>{title}</Item.Title
		>
		{#if sub}<span class={cn('min-w-0 shrink-[4] truncate text-xs text-muted-foreground', !stack && 'ml-2')} title={sub}>{sub}</span>{/if}
		{@render children?.()}
	</Item.Content>
	{#if meta}<span class={cn('shrink-0 text-xs whitespace-nowrap text-muted-foreground tabular-nums', metaClass)}>{meta}</span>{/if}
	{#if trailing}<span class="flex shrink-0 items-center gap-1.5">{@render trailing()}</span>{/if}
{/snippet}

<Item.Root size="xs" class={cls}>
	{#snippet child({ props })}
		{#if href}
			<a {href} {...props} data-row draggable="false" {@attach menuOf(items)}>{@render body()}</a>
		{:else if onclick}
			<button type="button" {onclick} {...props} data-row {@attach menuOf(items)}>{@render body()}</button>
		{:else}
			<div {...props} {@attach menuOf(items)}>{@render body()}</div>
		{/if}
	{/snippet}
</Item.Root>
