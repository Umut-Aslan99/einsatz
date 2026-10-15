<script lang="ts">
  import { enhance } from '$app/forms';
  import StatusChip from '$lib/components/StatusChip.svelte';
  import { fmtDate, fmtDateTime, fmtDuration, fmtMoney, fmtTime } from '$lib/format';

  // Job + ERP file come aggregated from the BFF load; actions run on the server and the page re-renders.
  let { data, form } = $props();
  const job = $derived(data.job);
  const xml = $derived(data.erpXml);
  let busy = $state(false);
  let returning = $state(false);

  const pending = () => {
    busy = true;
    return async ({ update }: { update: () => Promise<void> }) => { await update(); busy = false; returning = false; };
  };
</script>

<svelte:head><title>{job?.number ?? 'Bericht'} · Innendienst</title></svelte:head>

<a href="/buero" class="text-sm text-stone-500">← Alle Berichte</a>

{#if job}
  <div class="mt-3 flex flex-wrap items-start justify-between gap-4">
    <div>
      <p class="font-mono text-sm text-stone-500">{job.number} · {fmtDate(job.scheduledFor)} · {job.technicianName}</p>
      <h1 class="text-2xl font-bold">{job.customer.name}</h1>
      <p class="text-stone-600">{job.machine.name} <span class="font-mono text-sm">({job.machine.serialNumber})</span></p>
    </div>
    <div class="flex items-center gap-3">
      <StatusChip status={job.status} />
      {#if job.status !== 'Planned'}<a class="btn-ghost" href={job.reportUrl} target="_blank" rel="noopener">PDF ansehen</a>{/if}
    </div>
  </div>

  {#if form?.error}<p class="mt-4 rounded-xl bg-red-50 p-3 text-red-700" role="alert">{form.error}</p>{/if}

  <div class="mt-6 grid gap-4 lg:grid-cols-[1.6fr_1fr]">
    <div class="grid content-start gap-4">
      <section class="card p-5">
        <p class="label">Auftrag</p>
        <p class="mt-1">{job.task}</p>
        {#if job.notes}<p class="label mt-4">Bemerkungen des Technikers</p><p class="mt-1 whitespace-pre-line">{job.notes}</p>{/if}
      </section>

      <section class="card overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-stone-50 text-left text-xs uppercase tracking-wider text-stone-500">
            <tr><th class="px-4 py-2">Pos.</th><th class="px-4 py-2">Bezeichnung</th><th class="px-4 py-2 text-right">Menge</th><th class="px-4 py-2 text-right">Summe</th></tr>
          </thead>
          <tbody class="divide-y divide-stone-100 tabular-nums">
            <tr>
              <td class="px-4 py-2 font-mono text-xs">SRV-STD</td><td class="px-4 py-2">Arbeitszeit Techniker</td>
              <td class="px-4 py-2 text-right">{fmtDuration(job.totalMinutes)}</td><td class="px-4 py-2 text-right">{fmtMoney(Math.round(job.totalMinutes / 60 * 89 * 100) / 100)}</td>
            </tr>
            {#each job.materials as m (m.id)}
              <tr>
                <td class="px-4 py-2 font-mono text-xs">{m.articleNumber}</td><td class="px-4 py-2">{m.articleName}</td>
                <td class="px-4 py-2 text-right">{m.quantity} {m.unit}</td><td class="px-4 py-2 text-right">{fmtMoney(m.total)}</td>
              </tr>
            {/each}
          </tbody>
        </table>
        <p class="border-t border-stone-100 px-4 py-2 text-right text-xs text-stone-500">Stundensatz 89,00 € · Zeitblöcke:
          {job.timeEntries.map((t) => `${fmtTime(t.start)}–${t.end ? fmtTime(t.end) : '…'}`).join(', ') || '–'}</p>
      </section>

      {#if job.photos.length}
        <section class="card p-5">
          <p class="label">Fotos</p>
          <ul class="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
            {#each job.photos as p (p.id)}
              <li><a href={p.url} target="_blank" rel="noopener"><img src={p.url} alt={p.caption ?? 'Foto'} class="aspect-square w-full rounded-lg object-cover" /></a>
                {#if p.caption}<p class="mt-1 text-xs text-stone-600">{p.caption}</p>{/if}</li>
            {/each}
          </ul>
        </section>
      {/if}
    </div>

    <aside class="grid content-start gap-4">
      <section class="card p-5">
        <p class="label">Unterschrift</p>
        {#if job.signature}
          <img src="/api/files/jobs/{job.id}/signature" alt="Unterschrift {job.signature.signedBy}" class="mt-2 h-24 w-full object-contain" />
          <p class="border-t border-stone-200 pt-2 text-sm">{job.signature.signedBy} · {fmtDateTime(job.signature.signedAt)}</p>
        {:else}<p class="mt-1 text-stone-500">Noch nicht unterschrieben.</p>{/if}
      </section>

      {#if job.status === 'Completed'}
        <section class="card border-brand-500 p-5 ring-1 ring-brand-500" data-tour="review">
          <p class="font-semibold">Bericht prüfen</p>
          <p class="mt-1 text-sm text-stone-600">Freigabe erzeugt das PDF, legt es im DMS ab und übergibt Arbeitszeit und Material als Beleg an das ERP.</p>
          {#if returning}
            <form method="POST" action="?/return" use:enhance={pending}>
              <textarea name="reason" class="input mt-3 min-h-24 py-2" placeholder="Was muss korrigiert werden?" required></textarea>
              <div class="mt-3 grid grid-cols-2 gap-2">
                <button type="button" class="btn-ghost" onclick={() => (returning = false)}>Abbrechen</button>
                <button class="btn-dark" disabled={busy}>Zurückgeben</button>
              </div>
            </form>
          {:else}
            <form method="POST" action="?/approve" use:enhance={pending}>
              <button class="btn-primary mt-4 w-full" disabled={busy} data-testid="approve">
                {busy ? 'Wird übergeben …' : 'Freigeben & an ERP übergeben'}
              </button>
            </form>
            <button class="btn-ghost mt-2 w-full" onclick={() => (returning = true)}>An Techniker zurückgeben</button>
          {/if}
        </section>
      {/if}

      {#if job.integrations.length}
        <section class="card p-5" data-testid="integrations">
          <p class="label">Übergaben</p>
          <p class="mt-1 text-sm text-stone-600">Freigegeben von {job.approvedBy} am {job.approvedAt ? fmtDateTime(job.approvedAt) : ''}</p>
          <ul class="mt-3 grid gap-2">
            {#each job.integrations as i (i.id)}
              <li class="flex items-center gap-3 rounded-xl bg-stone-50 p-3">
                <span class="rounded-md bg-ink px-2 py-1 font-mono text-xs text-white">{i.target}</span>
                <a class="min-w-0 flex-1 truncate font-mono text-sm text-brand-700 hover:underline" href={i.url}>{i.fileName}</a>
              </li>
            {/each}
          </ul>
          {#if xml}
            <details class="mt-3">
              <summary class="cursor-pointer text-sm text-stone-600">ERP-Beleg (XML) ansehen</summary>
              <pre class="mt-2 max-h-80 overflow-auto rounded-xl bg-ink p-3 text-xs leading-relaxed text-stone-200">{xml}</pre>
            </details>
          {/if}
        </section>
      {/if}
    </aside>
  </div>
{/if}
