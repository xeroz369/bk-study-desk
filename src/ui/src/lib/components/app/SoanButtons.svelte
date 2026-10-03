<!-- Bộ nút Tạo, Nhập, Xuất cho một chỗ (môn, chương hoặc bài): mở trang Soạn đúng chỗ đó.
     path như scopePath (môn[/chương[/bài]]); where là chữ trong tooltip ("môn này", "chương này", "bài này"). -->
<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { LABELS } from '$lib/study/actions';

	let {
		path,
		where,
		can = { tao: true, nhap: true, xuat: true },
	}: { path: string; where: string; can?: { tao: boolean; nhap: boolean; xuat: boolean } } = $props();
</script>

{#if can.tao}<Button size="xs" variant="ghost" href={`#soan/tao/${path}`} title={`Tạo câu hỏi cho ${where}`}>{LABELS.button.create}</Button
	>{/if}
{#if can.nhap}<Button size="xs" variant="ghost" href={`#soan/nhap/${path}`} title={`Nhập quiz vào ${where}`}>{LABELS.button.import}</Button
	>{/if}
{#if can.xuat}<Button size="xs" variant="ghost" href={`#soan/xuat/${path}`} title={`Xuất ${where} để chia sẻ`}
		>{LABELS.button.export}</Button
	>{/if}
