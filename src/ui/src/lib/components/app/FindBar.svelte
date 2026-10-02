<!-- Tìm chữ trong trang (Ctrl+F), thay cho thanh tìm của trình duyệt đã tắt. Tô mọi kết quả bằng CSS Highlight API,
     Enter/F3 tới kết quả sau, Shift+Enter/Shift+F3 kết quả trước, Esc đóng. Chỉ tìm trong vùng nội dung (#content). -->
<script lang="ts">
	import { Input } from '$lib/components/ui/input';
	import { Button } from '$lib/components/ui/button';
	import ChevronUp from '@lucide/svelte/icons/chevron-up';
	import ChevronDown from '@lucide/svelte/icons/chevron-down';
	import X from '@lucide/svelte/icons/x';
	import { Desktop } from '$lib/desktop.svelte';
	import { contentEl, router } from '$lib/router.svelte';

	let query = $state('');
	let ranges: Range[] = [];
	let index = $state(-1);
	let count = $state(0);

	const hl = () => (CSS as unknown as { highlights?: Map<string, unknown> }).highlights;
	const Highlight = (window as unknown as { Highlight?: new (...r: Range[]) => unknown }).Highlight;

	function search() {
		ranges = [];
		const root = contentEl();
		const q = query.trim().toLocaleLowerCase('vi');
		if (root && q) {
			const walk = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
				acceptNode: (n) =>
					n.parentElement?.closest('[hidden], mjx-assistive-mml, script, style') || !n.parentElement?.offsetParent
						? NodeFilter.FILTER_REJECT
						: NodeFilter.FILTER_ACCEPT,
			});
			for (let n = walk.nextNode(); n; n = walk.nextNode()) {
				const text = (n.textContent ?? '').toLocaleLowerCase('vi');
				for (let i = text.indexOf(q); i >= 0; i = text.indexOf(q, i + q.length)) {
					const r = document.createRange();
					r.setStart(n, i);
					r.setEnd(n, i + q.length);
					ranges.push(r);
				}
			}
		}
		count = ranges.length;
		index = count ? 0 : -1;
		paint();
	}

	function paint() {
		const h = hl();
		if (!h || !Highlight) return;
		h.set('find', new Highlight(...ranges));
		h.set('find-current', index >= 0 ? new Highlight(ranges[index]) : new Highlight());
		if (index >= 0) ranges[index].startContainer.parentElement?.scrollIntoView({ block: 'center' });
	}

	function step(dir: 1 | -1) {
		if (!count) return;
		index = (index + dir + count) % count;
		paint();
	}

	function close() {
		Desktop.findOpen = false;
	}

	function onkey(e: KeyboardEvent) {
		if (e.key === 'Enter' || e.key === 'F3') {
			e.preventDefault();
			step(e.shiftKey ? -1 : 1);
		} else if (e.key === 'Escape') {
			e.preventDefault();
			close();
		}
	}

	// F3 khi ô tìm không có focus vẫn chạy (như Explorer, Edge).
	$effect(() => {
		const f3 = (e: KeyboardEvent) => {
			if (e.key === 'F3' && e.target !== document.getElementById('find-input')) {
				e.preventDefault();
				step(e.shiftKey ? -1 : 1);
			}
		};
		document.addEventListener('keydown', f3);
		return () => {
			document.removeEventListener('keydown', f3);
			hl()?.delete('find');
			hl()?.delete('find-current');
		};
	});

	// Đổi trang: tìm lại trên nội dung mới.
	$effect(() => {
		void router.route.name;
		void router.route.arg;
		const t = setTimeout(search, 400);
		return () => clearTimeout(t);
	});
</script>

<div class="flex shrink-0 items-center gap-1.5 border-b bg-background px-3 py-1" role="search">
	<Input
		id="find-input"
		class="h-7 w-64 text-sm"
		placeholder="Tìm trong trang"
		bind:value={query}
		oninput={search}
		onkeydown={onkey}
		aria-label="Tìm trong trang"
	/>
	<span class="w-20 text-xs text-muted-foreground tabular-nums" role="status"
		>{query.trim() ? (count ? `${index + 1}/${count}` : 'Không thấy') : ''}</span
	>
	<Button
		size="icon-xs"
		variant="ghost"
		title="Kết quả trước (Shift+F3)"
		aria-label="Kết quả trước"
		disabled={!count}
		onclick={() => step(-1)}><ChevronUp /></Button
	>
	<Button size="icon-xs" variant="ghost" title="Kết quả sau (F3)" aria-label="Kết quả sau" disabled={!count} onclick={() => step(1)}
		><ChevronDown /></Button
	>
	<Button size="icon-xs" variant="ghost" class="ml-auto" title="Đóng (Esc)" aria-label="Đóng tìm kiếm" onclick={close}><X /></Button>
</div>
