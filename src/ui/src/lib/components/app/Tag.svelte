<!-- Nhãn nhỏ duy nhất của app: Badge viền, màu chữ theo tone. Thay mọi nhãn tự viết (loại mốc, trạng thái, BB/TC…). -->
<script lang="ts" module>
	export type Tone = 'ok' | 'warn' | 'bad' | 'info' | 'muted' | 'primary' | 'assign' | 'quiz' | 'exam' | 'class' | 'reg' | 'event';
</script>

<script lang="ts">
	import { Badge } from '$lib/components/ui/badge';
	import { cn } from '$lib/utils';

	const TONE: Record<Tone, string> = {
		ok: 'text-ok border-ok/30 bg-ok/5',
		warn: 'text-warn border-warn/30 bg-warn/5',
		bad: 'text-bad border-bad/30 bg-bad/5',
		info: 'text-info border-info/30 bg-info/5',
		muted: 'text-muted-foreground border-border',
		primary: 'text-primary border-primary/30 bg-primary/5',
		assign: 'text-kind-assign border-kind-assign/30',
		quiz: 'text-kind-quiz border-kind-quiz/30',
		exam: 'text-kind-exam border-kind-exam/30',
		class: 'text-kind-class border-kind-class/30',
		reg: 'text-kind-reg border-kind-reg/30',
		event: 'text-kind-event border-kind-event/30',
	};
	let { tone = 'muted', text, title = '', class: className = '' }: { tone?: Tone; text: string; title?: string; class?: string } = $props();
</script>

<Badge variant="outline" {title} class={cn('h-[18px] rounded-md px-1.5 text-xs font-medium', TONE[tone], className)}>{text}</Badge>
