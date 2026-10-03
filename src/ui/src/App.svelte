<!-- Khung Luyện tập, nhúng trong trang Luyện tập của app WPF (Shell/WebUi.cs): thanh đường dẫn của trang · InfoBar · thanh tìm ·
     vùng nội dung tự cuộn. Trang theo #hash; chỉ dựng lại khi đổi trang. Liên kết ra ngoài phần Luyện tập: báo app chuyển trang. -->
<script lang="ts">
	import { onMount } from 'svelte';
	import { ModeWatcher } from 'mode-watcher';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import InfoBar from '$lib/components/app/InfoBar.svelte';
	import FindBar from '$lib/components/app/FindBar.svelte';
	import AppMenu from '$lib/components/app/AppMenu.svelte';
	import { api, errorText } from '$lib/api/client';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';
	import { Desktop, onKey, onWheel, toApp } from '$lib/desktop.svelte';
	import { router } from '$lib/router.svelte';
	import { PAGES } from '$lib/routes';

	let ready = $state(false);
	const known = $derived(!!PAGES[router.route.name]);
	const page = $derived(PAGES[router.route.name] ?? PAGES['luyen-tap']);
	const pageModule = $derived(page.load());
	// Trang đọc (bài, đề, ôn câu sai, trang tĩnh): một cột giữa, thanh đường dẫn thẳng hàng với cột.
	const reading = $derived(['bai', 'thi', 'on-cau-sai', 'trang'].includes(router.route.name));

	// Trang không thuộc Luyện tập (Môn học, Lịch…): app WPF hiện, khung này quay lại trang trước.
	$effect(() => {
		const r = router.route;
		if (known) return;
		toApp({ type: 'navigate', route: r.name + (r.arg ? '/' + r.arg : '') });
		history.back();
	});
	$effect(() => {
		if (Progress.saveError) Desktop.inform({ id: 'save', tone: 'bad', text: Progress.saveError });
		else Desktop.dismiss('save');
	});

	onMount(() => {
		void (async () => {
			await Promise.all([Study.load(), Progress.init()]);
			Progress.seedSchedule();
			ready = true;
		})();
		// Link ra ngoài trong bài học: mở qua app (web trường trong cửa sổ của app, trang khác ra trình duyệt).
		const onClick = (e: MouseEvent) => {
			const a = (e.target as HTMLElement).closest('a[href^="http"]') as HTMLAnchorElement | null;
			if (!a) return;
			e.preventDefault();
			api.openWeb(a.href, a.textContent?.trim() ?? '').catch(() => Desktop.say('Không mở được liên kết'));
		};
		// Lỗi async không ai bắt (lệnh trong menu, nút bấm): hiện InfoBar thay vì im lặng. Không preventDefault để console vẫn có stack.
		const onRejection = (e: PromiseRejectionEvent) =>
			Desktop.inform({ id: 'unhandled', tone: 'bad', text: `Có lỗi chưa xử lý: ${errorText(e.reason)}. Thử lại thao tác vừa làm.` });
		document.addEventListener('keydown', onKey);
		document.addEventListener('click', onClick);
		window.addEventListener('unhandledrejection', onRejection);
		return () => {
			document.removeEventListener('keydown', onKey);
			document.removeEventListener('click', onClick);
			window.removeEventListener('unhandledrejection', onRejection);
		};
	});
</script>

<ModeWatcher />

<div class="flex h-svh min-h-0 flex-col overflow-hidden">
	<!-- Thanh đường dẫn: PageShell của trang chuyển vào đây (không cuộn theo nội dung). -->
	<div id="toolbar" class="shrink-0 border-b {reading ? 'reading' : ''}"></div>
	{#if Desktop.infos.length}
		<div class="shrink-0 px-3 pt-2">
			{#each Desktop.infos as m (m.id)}<InfoBar tone={m.tone} text={m.text} onclose={() => (m.onclose?.(), Desktop.dismiss(m.id))} />{/each}
		</div>
	{/if}
	{#if Desktop.findOpen}<FindBar />{/if}
	<AppMenu>
		{#snippet children(props)}
			<main
				{...props}
				id="content"
				class="min-h-0 flex-1 overflow-auto p-3 [scrollbar-gutter:stable]"
				style:zoom={Desktop.zoom}
				{@attach (n) => {
					n.addEventListener('wheel', onWheel, { passive: false });
					return () => n.removeEventListener('wheel', onWheel);
				}}
			>
				{#if !ready}
					<Skeleton class="h-40 w-full" />
				{:else}
					{#await pageModule}
						<Skeleton class="h-40 w-full" />
					{:then mod}
						<div class={reading ? 'reading-col' : ''}>
							{#key router.route.name + (page.keyArg ? '/' + router.route.arg : '')}
								<mod.default arg={router.route.arg} />
							{/key}
						</div>
					{/await}
				{/if}
			</main>
		{/snippet}
	</AppMenu>
</div>
