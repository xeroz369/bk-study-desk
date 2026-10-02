<!-- InfoBar kiểu Windows: một dòng trong bố cục (không nổi), biểu tượng + câu + nút sửa + nút đóng.
     Dùng cho trạng thái cần người dùng biết nhưng không chặn việc: đồng bộ lỗi, đang đăng nhập, lưu lỗi. Dựng trên Alert. -->
<script lang="ts">
	import type { Snippet } from 'svelte';
	import * as Alert from '$lib/components/ui/alert';
	import { Button } from '$lib/components/ui/button';
	import Info from '@lucide/svelte/icons/info';
	import TriangleAlert from '@lucide/svelte/icons/triangle-alert';
	import CircleX from '@lucide/svelte/icons/circle-x';
	import X from '@lucide/svelte/icons/x';
	import { cn } from '$lib/utils';

	let {
		tone = 'info',
		text,
		onclose,
		children,
	}: { tone?: 'info' | 'warn' | 'bad'; text: string; onclose?: () => void; children?: Snippet } = $props();
	const TONE = { info: 'border-info/30 bg-info/5', warn: 'border-warn/30 bg-warn-bg', bad: 'border-bad/30 bg-bad-bg' };
	const ICON = { info: 'text-info', warn: 'text-warn', bad: 'text-bad' };
</script>

<Alert.Root class={cn('mb-2 flex items-center gap-2 rounded-md px-2.5 py-1', TONE[tone])} role={tone === 'info' ? 'status' : 'alert'}>
	{#if tone === 'bad'}<CircleX class="size-4 shrink-0 {ICON[tone]}" />{:else if tone === 'warn'}<TriangleAlert
			class="size-4 shrink-0 {ICON[tone]}"
		/>{:else}<Info class="size-4 shrink-0 {ICON[tone]}" />{/if}
	<Alert.Description class="min-w-0 flex-1 text-sm text-foreground">{text}</Alert.Description>
	{@render children?.()}
	{#if onclose}<Button size="icon-xs" variant="ghost" onclick={onclose} title="Đóng" aria-label="Đóng"><X /></Button>{/if}
</Alert.Root>
