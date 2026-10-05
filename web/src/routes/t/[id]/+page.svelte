<script lang="ts">
  import { page } from '$app/state';
  import MaterialPicker from '$lib/components/MaterialPicker.svelte';
  import SignaturePad from '$lib/components/SignaturePad.svelte';
  import StatusChip from '$lib/components/StatusChip.svelte';
  import { canComplete, minutes } from '$lib/offline/apply';
  import { field } from '$lib/offline/field.svelte';
  import { fmtDate, fmtDuration, fmtMoney, fmtTime } from '$lib/format';
  import { compressImage } from '$lib/image';

  const job = $derived(field.job(page.params.id!));
  const editable = $derived(job?.status === 'Planned' || job?.status === 'InProgress');
  const running = $derived(job?.timeEntries.find((t) => t.end === null));
  const check = $derived(job ? canComplete(job) : { ok: false, missing: [] });

  let now = $state(Date.now());
  $effect(() => {
    if (!running) return;
    const t = setInterval(() => (now = Date.now()), 15_000);
    return () => clearInterval(t);
  });

  let picking = $state(false);
  let notes = $state<string | null>(null);
  let signer = $state('');
  let pad = $state<SignaturePad>();
  let padEmpty = $state(true);
  let photoBusy = $state(false);
  let photoCaption = $state('');

  const record = field.record.bind(field);

  async function onPhoto(e: Event) {
    const input = e.currentTarget as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || !job) return;
    photoBusy = true;
    try {
      const img = await compressImage(file);
      const caption = photoCaption.trim() || null;
      photoCaption = '';
      await record(job.id, 'addPhoto', { photoId: crypto.randomUUID(), ...img, caption });
    } finally {
      photoBusy = false;
    }
  }

  async function saveNotes() {
    if (job && notes !== null && notes !== (job.notes ?? '')) await record(job.id, 'setNotes', { text: notes });
  }

  async function sign() {
    if (!job || !pad) return;
    await record(job.id, 'sign', { signedBy: signer, pngBase64: pad.toBase64() });
    pad.clear();
  }

  const mapsUrl = (j: NonNullable<typeof job>) =>
    `https://www.openstreetmap.org/search?query=${encodeURIComponent(`${j.customer.street}, ${j.customer.city}`)}`;
</script>

<svelte:head><title>{job?.number ?? 'Auftrag'} · Einsatz</title></svelte:head>

<a href="/t" class="text-sm text-stone-500">← Alle Einsätze</a>

{#if !job}
  <p class="card mt-4 p-6 text-stone-500">Auftrag nicht gefunden.</p>
{:else}
  <div class="mt-3 flex items-start justify-between gap-3">
    <div>
      <p class="font-mono text-sm text-stone-500">{job.number} · {fmtDate(job.scheduledFor)}</p>
      <h1 class="text-2xl font-bold leading-tight">{job.customer.name}</h1>
    </div>
    <StatusChip status={job.status} />
  </div>

  {#if job.returnReason && editable}
    <p class="mt-4 rounded-xl bg-red-50 p-3 text-sm text-red-800 ring-1 ring-red-200">
      <strong>Vom Innendienst zurückgegeben:</strong> {job.returnReason} – bitte korrigieren und erneut unterschreiben lassen.
    </p>
  {/if}

  <section class="card mt-4 grid gap-4 p-4 sm:grid-cols-2">
    <div>
      <p class="label">Kunde</p>
      <p class="mt-1">{job.customer.street}<br />{job.customer.city}</p>
      <a class="mt-1 inline-block text-sm font-medium text-brand-700" href={mapsUrl(job)} target="_blank" rel="noopener">Route öffnen ↗</a>
    </div>
    <div>
      <p class="label">Anlage</p>
      <p class="mt-1 font-medium">{job.machine.name}</p>
      <p class="font-mono text-sm text-stone-500">{job.machine.serialNumber}</p>
    </div>
    <div class="sm:col-span-2">
      <p class="label">Auftrag</p>
      <p class="mt-1">{job.task}</p>
    </div>
  </section>

  <!-- Arbeitszeit -->
  <section class="card mt-4 p-4">
    <div class="flex items-center justify-between">
      <h2 class="font-bold">Arbeitszeit</h2>
      <span class="font-mono text-sm tabular-nums text-stone-600">
        {fmtDuration(job.timeEntries.reduce((n, t) => n + minutes(t, now), 0))}
      </span>
    </div>
    {#if job.timeEntries.length}
      <ul class="mt-3 divide-y divide-stone-100 text-sm">
        {#each job.timeEntries as t (t.id)}
          <li class="flex justify-between py-2 tabular-nums">
            <span>{fmtTime(t.start)} – {t.end ? fmtTime(t.end) : 'läuft'}</span>
            <span class="text-stone-500">{minutes(t, now)} min</span>
          </li>
        {/each}
      </ul>
    {/if}
    {#if editable}
      {#if running}
        <button class="btn-dark mt-3 w-full" onclick={() => record(job.id, 'stopWork')} data-testid="stop">■ Arbeit stoppen</button>
      {:else}
        <button class="btn-primary mt-3 w-full" onclick={() => record(job.id, 'startWork')} data-testid="start">▶ Arbeit starten</button>
      {/if}
    {/if}
  </section>

  <!-- Material -->
  <section class="card mt-4 p-4">
    <div class="flex items-center justify-between">
      <h2 class="font-bold">Material</h2>
      <span class="text-sm tabular-nums text-stone-600">{fmtMoney(job.materialTotal)}</span>
    </div>
    {#if job.materials.length}
      <ul class="mt-3 divide-y divide-stone-100">
        {#each job.materials as m (m.id)}
          <li class="flex items-center gap-3 py-2">
            <span class="w-14 shrink-0 text-right font-semibold tabular-nums">{m.quantity}</span>
            <span class="w-8 text-sm text-stone-500">{m.unit}</span>
            <span class="min-w-0 flex-1"><span class="block truncate">{m.articleName}</span><span class="font-mono text-xs text-stone-500">{m.articleNumber}</span></span>
            {#if editable}
              <button class="size-10 rounded-lg text-stone-400 hover:bg-red-50 hover:text-red-600" aria-label="Entfernen"
                onclick={() => record(job.id, 'removeMaterial', { lineId: m.id })}>✕</button>
            {/if}
          </li>
        {/each}
      </ul>
    {/if}
    {#if editable}
      <button class="btn-ghost mt-3 w-full" onclick={() => (picking = true)} data-testid="add-material">+ Material hinzufügen</button>
    {/if}
  </section>

  <!-- Fotos -->
  <section class="card mt-4 p-4">
    <h2 class="font-bold">Fotos <span class="font-normal text-stone-500">· {job.photos.length}/8</span></h2>
    {#if job.photos.length}
      <ul class="mt-3 grid grid-cols-3 gap-2">
        {#each job.photos as p (p.id)}
          <li class="overflow-hidden rounded-lg bg-stone-100">
            <img src={p.url} alt={p.caption ?? 'Foto'} class="aspect-square w-full object-cover" loading="lazy" />
            {#if p.caption}<p class="truncate px-2 py-1 text-xs text-stone-600">{p.caption}</p>{/if}
          </li>
        {/each}
      </ul>
    {/if}
    {#if editable && job.photos.length < 8}
      <input class="input mt-3" placeholder="Bildunterschrift (optional), dann Foto aufnehmen" bind:value={photoCaption} />
      <label class="btn-ghost mt-3 w-full cursor-pointer {photoBusy ? 'opacity-50' : ''}">
        📷 {photoBusy ? 'Wird verarbeitet …' : 'Foto aufnehmen'}
        <input type="file" accept="image/*" capture="environment" class="sr-only" onchange={onPhoto} disabled={photoBusy} />
      </label>
    {/if}
  </section>

  <!-- Notizen -->
  <section class="card mt-4 p-4">
    <h2 class="font-bold">Bemerkungen</h2>
    {#if editable}
      <textarea class="input mt-3 min-h-28 py-2" placeholder="Befund, durchgeführte Arbeiten, Hinweise für den nächsten Termin …"
        value={notes ?? job.notes ?? ''} oninput={(e) => (notes = e.currentTarget.value)} onblur={saveNotes} data-testid="notes"></textarea>
    {:else}
      <p class="mt-2 whitespace-pre-line text-stone-700">{job.notes ?? '–'}</p>
    {/if}
  </section>

  <!-- Unterschrift -->
  <section class="card mt-4 p-4">
    <h2 class="font-bold">Unterschrift Kunde</h2>
    {#if job.signature}
      <p class="mt-2 rounded-xl bg-emerald-50 p-3 text-emerald-800">
        ✓ Unterschrieben von <strong>{job.signature.signedBy}</strong> um {fmtTime(job.signature.signedAt)}
      </p>
    {/if}
    {#if editable}
      <p class="mt-2 text-sm text-stone-600">Der Kunde bestätigt, dass die Arbeiten ordnungsgemäß ausgeführt wurden.</p>
      <input class="input mt-3" placeholder="Name des Unterzeichners" bind:value={signer} data-testid="signer" />
      <div class="mt-3"><SignaturePad bind:this={pad} onchange={(e) => (padEmpty = e)} /></div>
      <div class="mt-3 grid grid-cols-2 gap-3">
        <button class="btn-ghost" onclick={() => pad?.clear()}>Löschen</button>
        <button class="btn-dark" disabled={padEmpty || !signer.trim()} onclick={sign} data-testid="sign">
          {job.signature ? 'Erneut unterschreiben' : 'Unterschrift übernehmen'}
        </button>
      </div>
    {/if}
  </section>

  {#if editable}
    <div class="fixed inset-x-0 bottom-0 z-10 border-t border-stone-200 bg-white/95 p-3 backdrop-blur" style="padding-bottom: max(0.75rem, env(safe-area-inset-bottom))">
      <div class="mx-auto flex max-w-3xl items-center gap-3">
        <p class="min-w-0 flex-1 truncate text-sm text-stone-600">
          {check.ok ? 'Alles erfasst.' : `Fehlt: ${check.missing.join(', ')}`}
        </p>
        <button class="btn-primary px-6" disabled={!check.ok} onclick={() => record(job.id, 'complete')} data-testid="complete">Abschließen</button>
      </div>
    </div>
  {:else}
    <p class="mt-6 rounded-xl bg-stone-200/60 p-4 text-center text-stone-700">
      {job.status === 'Completed' ? 'Bericht abgeschlossen – liegt beim Innendienst zur Prüfung.' : 'Bericht freigegeben und an das ERP übergeben.'}
    </p>
  {/if}

  {#if picking}
    <MaterialPicker articles={field.articles} onclose={() => (picking = false)}
      onpick={(a, quantity) => record(job.id, 'addMaterial', { lineId: crypto.randomUUID(), articleNumber: a.number, quantity })} />
  {/if}
{/if}
