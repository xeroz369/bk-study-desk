<!-- Question editor: 5 types (like LMS quizzes), Markdown + TeX text, images by paste / drop / button.
     Images are compressed (image.ts) and referenced as img/anh-N.webp; on save they become data URIs in the pack. -->
<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import * as Alert from '$lib/components/ui/alert';
	import { typeset } from '$lib/math';
	import { mdToHtml } from '$lib/study/markdown';
	import { sanitizeHtml } from '$lib/study/sanitize';
	import { compressImage, imageFrom } from '$lib/study/image';
	import { parseNumber } from '$lib/study/grade';
	import type { PackQuestion, PackQuestionType } from '$lib/study/pack';

	let { onsave }: { onsave: (q: PackQuestion) => Promise<string[]> } = $props();

	const TYPES: [PackQuestionType, string][] = [
		['single', 'Một đáp án'],
		['multi', 'Nhiều đáp án'],
		['truefalse', 'Đúng / Sai'],
		['numeric', 'Điền số'],
		['short', 'Điền chữ'],
	];
	const L = 'ABCDEFGH';
	let type = $state<PackQuestionType>('single');
	let prompt = $state('');
	let options = $state([
		{ text: '', right: true },
		{ text: '', right: false },
		{ text: '', right: false },
		{ text: '', right: false },
	]);
	let truth = $state(true);
	let value = $state(''),
		tolerance = $state(''),
		unit = $state('');
	let accept = $state('');
	let solution = $state('');
	let tag = $state('');
	let keepOrder = $state(false);
	let images = $state<Record<string, string>>({});
	let errors = $state<string[]>([]);
	let saving = $state(false);
	let filePick = $state<HTMLInputElement>();
	let lastField: { el: HTMLInputElement | HTMLTextAreaElement; set: (v: string) => void } | null = null;

	const render = (md: string) => sanitizeHtml(mdToHtml(md, (src) => images[src] ?? src));

	// ---- images: paste / drop into any field, or the "Chèn ảnh" button (goes to the last focused field)
	async function addImage(file: Blob, el: HTMLInputElement | HTMLTextAreaElement, set: (v: string) => void) {
		const name = `img/anh-${Object.keys(images).length + 1}.webp`;
		images[name] = await compressImage(file);
		const at = el.selectionStart ?? el.value.length;
		const ref = el instanceof HTMLTextAreaElement ? `\n![](${name})\n` : ` ![](${name})`;
		set(el.value.slice(0, at) + ref + el.value.slice(el.selectionEnd ?? at));
	}
	function imageField(set: (v: string) => void) {
		return (node: HTMLInputElement | HTMLTextAreaElement) => {
			const focus = () => (lastField = { el: node, set });
			const paste = (e: ClipboardEvent) => {
				const f = imageFrom(e.clipboardData);
				if (f) {
					e.preventDefault();
					void addImage(f, node, set);
				}
			};
			const drop = (e: DragEvent) => {
				const f = imageFrom(e.dataTransfer);
				if (f) {
					e.preventDefault();
					void addImage(f, node, set);
				}
			};
			const el = node as HTMLElement; // union types lose the per-event overloads of addEventListener
			el.addEventListener('focus', focus);
			el.addEventListener('paste', paste);
			el.addEventListener('drop', drop);
			return () => {
				el.removeEventListener('focus', focus);
				el.removeEventListener('paste', paste);
				el.removeEventListener('drop', drop);
			};
		};
	}
	async function pickImage(e: Event) {
		const input = e.currentTarget as HTMLInputElement;
		const f = input.files?.[0];
		input.value = '';
		if (f && lastField) await addImage(f, lastField.el, lastField.set);
		else if (f) {
			const el = document.getElementById('qe-prompt') as HTMLTextAreaElement;
			await addImage(f, el, (v) => (prompt = v));
		}
	}

	// ---- build + save
	function build(): PackQuestion | string {
		if (!prompt.trim()) return 'Chưa có đề bài.';
		if (!solution.trim()) return 'Chưa có lời giải (để người khác tự học được).';
		const base = { ...(tag.trim() ? { tag: tag.trim() } : {}), prompt: render(prompt), solution: render(solution) };
		if (type === 'single' || type === 'multi') {
			const opts = options.filter((o) => o.text.trim());
			if (opts.length < 2) return 'Cần ít nhất 2 phương án.';
			const right = opts.map((o, i) => (o.right ? i : -1)).filter((i) => i >= 0);
			if (!right.length) return 'Chưa chọn phương án đúng (chọn chữ cái A, B...).';
			const html = opts.map((o) => render(o.text).replace(/^<p>([\s\S]*)<\/p>$/, '$1'));
			const keep = keepOrder ? { keepOrder: true } : {};
			return type === 'single'
				? { ...base, ...keep, options: html, answer: right[0] }
				: { ...base, ...keep, type: 'multi', options: html, answers: right };
		}
		if (type === 'truefalse') return { ...base, type, answer: truth };
		if (type === 'numeric') {
			const v = parseNumber(value),
				t = tolerance.trim() ? parseNumber(tolerance) : 0;
			if (v === null) return 'Đáp số chưa phải số (vd. 0,1875).';
			if (t === null || t < 0) return 'Sai số cho phép phải là số ≥ 0.';
			return { ...base, type, answer: v, ...(t ? { tolerance: t } : {}), ...(unit.trim() ? { unit: unit.trim() } : {}) };
		}
		const acc = accept
			.split('|')
			.map((s) => s.trim())
			.filter(Boolean);
		return acc.length ? { ...base, type, accept: acc } : 'Chưa có đáp án chữ.';
	}
	async function save() {
		const q = build();
		if (typeof q === 'string') {
			errors = [q];
			return;
		}
		saving = true;
		errors = await onsave(q);
		saving = false;
	}

	function setType(t: PackQuestionType) {
		type = t;
		if (t === 'single' && options.filter((o) => o.right).length > 1)
			options = options.map((o, i) => ({ ...o, right: i === options.findIndex((x) => x.right) }));
	}
	function markRight(i: number) {
		options = options.map((o, j) => ({ ...o, right: type === 'multi' ? (j === i ? !o.right : o.right) : j === i }));
	}
	const preview = $derived.by(() => {
		const parts = [render(prompt)];
		if (type === 'single' || type === 'multi')
			parts.push(
				...options
					.filter((o) => o.text.trim())
					.map(
						(o, i) => `<p>${o.right ? '<b>' : ''}${L[i]}. ${render(o.text).replace(/^<p>|<\/p>$/g, '')}${o.right ? ' (đúng)</b>' : ''}</p>`,
					),
			);
		else if (type === 'truefalse') parts.push(`<p><b>Đáp án: ${truth ? 'Đúng' : 'Sai'}</b></p>`);
		else if (type === 'numeric') parts.push(`<p><b>Đáp số: ${value}${tolerance ? ' ± ' + tolerance : ''} ${unit}</b></p>`);
		else
			parts.push(
				`<p><b>Chấp nhận: ${accept
					.split('|')
					.map((s) => s.trim())
					.filter(Boolean)
					.join(' / ')}</b></p>`,
			);
		if (solution.trim()) parts.push(`<p><i>Lời giải:</i></p>${render(solution)}`);
		return parts.join('');
	});
</script>

<div class="flex flex-col gap-3 text-sm">
	<div class="flex flex-wrap gap-1.5" role="radiogroup" aria-label="Loại câu">
		{#each TYPES as [t, label] (t)}
			<Button size="xs" variant={type === t ? 'default' : 'outline'} role="radio" aria-checked={type === t} onclick={() => setType(t)}
				>{label}</Button
			>
		{/each}
	</div>
	<label class="flex flex-col gap-1">
		<span>Đề bài <span class="text-xs text-muted-foreground">Markdown; công thức \( ... \); dán (Ctrl+V) hoặc kéo ảnh vào ô</span></span>
		<Textarea
			id="qe-prompt"
			bind:value={prompt}
			rows={3}
			{@attach imageField((v) => (prompt = v))}
			placeholder={'Ví dụ: Cho \\(f(x)=x^3-2\\) trên \\([1,2]\\). Theo chia đôi, \\(x_2\\) là'}
		/>
	</label>

	{#if type === 'single' || type === 'multi'}
		<div class="flex flex-col gap-1.5">
			<span
				>Phương án <span class="text-xs text-muted-foreground"
					>chọn chữ cái để đánh dấu {type === 'multi' ? 'các phương án đúng' : 'phương án đúng'}</span
				></span
			>
			{#each options as o, i (i)}
				<div class="flex items-center gap-2">
					<Button
						size="xs"
						variant={o.right ? 'default' : 'outline'}
						onclick={() => markRight(i)}
						aria-pressed={o.right}
						aria-label={`Phương án ${L[i]} đúng`}>{L[i]}</Button
					>
					<Input bind:value={o.text} {@attach imageField((v) => (options[i].text = v))} placeholder={'Phương án ' + L[i]} />
					{#if options.length > 2}<Button size="xs" variant="ghost" onclick={() => options.splice(i, 1)}>Bỏ</Button>{/if}
				</div>
			{/each}
			<div class="flex gap-2">
				{#if options.length < 8}<Button size="xs" variant="ghost" onclick={() => options.push({ text: '', right: false })}
						>Thêm phương án</Button
					>{/if}
				<Button size="xs" variant={keepOrder ? 'default' : 'ghost'} aria-pressed={keepOrder} onclick={() => (keepOrder = !keepOrder)}
					>Giữ thứ tự khi xáo</Button
				>
			</div>
		</div>
	{:else if type === 'truefalse'}
		<div class="flex gap-2" role="radiogroup" aria-label="Đáp án">
			<Button size="sm" variant={truth ? 'default' : 'outline'} role="radio" aria-checked={truth} onclick={() => (truth = true)}
				>Đúng</Button
			>
			<Button size="sm" variant={!truth ? 'default' : 'outline'} role="radio" aria-checked={!truth} onclick={() => (truth = false)}
				>Sai</Button
			>
		</div>
	{:else if type === 'numeric'}
		<div class="flex flex-wrap items-end gap-3">
			<label class="flex flex-col gap-1">Đáp số<Input bind:value class="w-36" inputmode="decimal" placeholder="0,1875" /></label>
			<label class="flex flex-col gap-1"
				>Sai số cho phép<Input bind:value={tolerance} class="w-32" inputmode="decimal" placeholder="0,0001" /></label
			>
			<label class="flex flex-col gap-1">Đơn vị (nếu có)<Input bind:value={unit} class="w-28" placeholder="m/s" /></label>
		</div>
	{:else}
		<label class="flex flex-col gap-1"
			>Đáp án chữ <span class="text-xs text-muted-foreground">nhiều cách viết đúng thì ngăn bằng dấu |, không phân biệt hoa thường</span>
			<Input bind:value={accept} placeholder="Newton | Newton-Raphson" />
		</label>
	{/if}

	<label class="flex flex-col gap-1"
		>Lời giải <span class="text-xs text-muted-foreground">đủ để tự làm lại: công thức, các bước</span>
		<Textarea bind:value={solution} rows={3} {@attach imageField((v) => (solution = v))} />
	</label>
	<label class="flex flex-col gap-1"
		>Nguồn (không bắt buộc)<Input bind:value={tag} placeholder="Slide tr.36, GK251 câu 8, Tự tạo..." /></label
	>

	<div class="flex flex-wrap items-center gap-2">
		<Button size="sm" onclick={save} disabled={saving}>Lưu câu vào bài</Button>
		<Button size="sm" variant="outline" onclick={() => filePick?.click()}>Chèn ảnh...</Button>
		<span class="text-xs text-muted-foreground">ảnh tự thu nhỏ, nén WebP</span>
		<input bind:this={filePick} type="file" accept="image/png,image/jpeg,image/webp,image/gif" class="hidden" onchange={pickImage} />
	</div>
	{#if errors.length}
		<Alert.Root variant="destructive"
			><Alert.Title>Chưa lưu được</Alert.Title><Alert.Description
				><ul class="list-disc pl-4">
					{#each errors as e, i (i)}<li>{e}</li>{/each}
				</ul></Alert.Description
			></Alert.Root
		>
	{/if}
	{#if prompt.trim()}
		<div class="rounded-lg border p-3">
			<p class="mb-1 text-xs text-muted-foreground">Xem trước</p>
			<div class="prose-lesson" use:typeset={preview}>{@html preview}</div>
		</div>
	{/if}
</div>
