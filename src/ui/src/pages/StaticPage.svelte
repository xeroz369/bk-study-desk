<!-- Trang tĩnh viết trong content/pages (ví dụ #trang/lo-trinh). -->
<script lang="ts">
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import { typeset } from '$lib/math';
	import { Study } from '$lib/study/registry.svelte';

	let { arg }: { arg: string } = $props();
	const page = $derived(Study.pages[arg]);
	const title = $derived(Study.manifest.pages?.find((p) => p.id === arg)?.title ?? 'Trang');
</script>

<PageShell {title} crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]} />
<Panel pad>
	{#if page}
		<div class="prose-lesson" use:typeset={arg}>{@html page.html}</div>
	{:else}
		<p class="text-sm text-muted-foreground">Chưa có nội dung.</p>
	{/if}
</Panel>
