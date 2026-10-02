<!-- Trạng thái trống trong một khối: một dòng mờ, nói bước tiếp theo. -->
<script lang="ts">
	import type { Snippet } from 'svelte';
	let { children }: { children: Snippet } = $props();
</script>

<p class="px-row py-1.5 text-xs text-muted-foreground">{@render children()}</p>
