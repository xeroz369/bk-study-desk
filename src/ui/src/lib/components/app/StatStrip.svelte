<!-- Dải số liệu một hàng (thay ô số to): nhãn nhỏ · số · ghi chú. Ô có href thì bấm được. -->
<script lang="ts" module>
	export interface StatItem {
		label: string;
		value: string | number;
		note?: string;
		href?: string;
		tone?: 'ok' | 'warn' | 'bad' | 'primary';
	}
</script>

<script lang="ts">
	import { cn } from '$lib/utils';
	const TONE = { ok: 'text-ok', warn: 'text-warn', bad: 'text-bad', primary: 'text-primary' };
	let { items, class: className = '' }: { items: StatItem[]; class?: string } = $props();
</script>

<div class={cn('flex flex-wrap divide-x overflow-hidden rounded-lg border bg-card', className)}>
	{#each items as s (s.label)}
		<svelte:element
			this={s.href ? 'a' : 'div'}
			href={s.href}
			class="flex min-w-32 flex-1 flex-col justify-center px-3 py-1.5 {s.href ? 'hover:bg-muted/50' : ''}"
		>
			<span class="text-xs leading-tight text-muted-foreground">{s.label}</span>
			<span class="flex flex-wrap items-baseline gap-x-1.5">
				<span class="text-lg leading-6 font-semibold tabular-nums {s.tone ? TONE[s.tone] : ''}">{s.value}</span>
				{#if s.note}<span class="text-xs text-muted-foreground">{s.note}</span>{/if}
			</span>
		</svelte:element>
	{/each}
</div>
