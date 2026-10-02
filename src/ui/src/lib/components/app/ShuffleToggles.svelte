<!-- "Xáo câu" / "Xáo đáp án" toggles. The choice is remembered on this device (shuffle.ts). -->
<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { savePrefs, type ShufflePrefs } from '$lib/study/shuffle';

	let { prefs = $bindable() }: { prefs: ShufflePrefs } = $props();
	function flip(key: keyof ShufflePrefs) {
		prefs = { ...prefs, [key]: !prefs[key] };
		savePrefs(prefs);
	}
</script>

<Button size="xs" variant={prefs.questions ? 'default' : 'ghost'} aria-pressed={prefs.questions} onclick={() => flip('questions')}
	>Xáo câu</Button
>
<Button size="xs" variant={prefs.options ? 'default' : 'ghost'} aria-pressed={prefs.options} onclick={() => flip('options')}
	>Xáo đáp án</Button
>
