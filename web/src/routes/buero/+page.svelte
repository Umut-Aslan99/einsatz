<script lang="ts">
  import StatusChip from '$lib/components/StatusChip.svelte';
  import { fmtDate, fmtDuration, fmtMoney } from '$lib/format';
  import type { JobStatus } from '$lib/types';

  // Rows are a BFF view model, server-rendered: no GraphQL and no loading spinner in the browser.
  let { data } = $props();
  const jobs = $derived(data.rows);
  let tab = $state<JobStatus | 'all'>('Completed');

  const count = (s: JobStatus) => jobs.filter((j) => j.status === s).length;
  const tabs: { id: JobStatus | 'all'; label: string }[] = [
    { id: 'Completed', label: 'Zur Prüfung' }, { id: 'InProgress', label: 'In Arbeit' },
    { id: 'Planned', label: 'Geplant' }, { id: 'Exported', label: 'Im ERP' }, { id: 'all', label: 'Alle' },
  ];
  const visible = $derived(tab === 'all' ? jobs : jobs.filter((j) => j.status === tab));
  const kpis = $derived([
    { label: 'Zur Prüfung', value: String(count('Completed')), accent: true },
    { label: 'Heute unterwegs', value: String(jobs.filter((j) => j.status === 'InProgress' || j.status === 'Planned').length) },
    { label: 'Im ERP', value: String(count('Exported')) },
    { label: 'Material in Prüfung', value: fmtMoney(jobs.filter((j) => j.status === 'Completed').reduce((n, j) => n + j.materialTotal, 0)) },
  ]);
</script>

<svelte:head><title>Innendienst · Einsatz</title></svelte:head>

<h1 class="text-2xl font-bold">Serviceberichte</h1>
<p class="text-stone-600">Abgeschlossene Einsätze prüfen und an ERP und DMS übergeben.</p>

<dl class="mt-6 grid grid-cols-2 gap-3 lg:grid-cols-4" data-tour="office-kpis">
  {#each kpis as k (k.label)}
    <div class="card p-4 {k.accent ? 'border-brand-500 ring-1 ring-brand-500' : ''}">
      <dt class="label">{k.label}</dt>
      <dd class="mt-1 text-2xl font-bold tabular-nums {k.accent ? 'text-brand-700' : ''}">{k.value}</dd>
    </div>
  {/each}
</dl>

<nav class="mt-8 flex gap-1 overflow-x-auto border-b border-stone-200" aria-label="Status">
  {#each tabs as t (t.id)}
    <button class="-mb-px whitespace-nowrap border-b-2 px-4 py-2.5 text-sm font-medium
      {tab === t.id ? 'border-brand-500 text-ink' : 'border-transparent text-stone-500 hover:text-ink'}"
      onclick={() => (tab = t.id)} aria-current={tab === t.id}>
      {t.label}{#if t.id !== 'all'}<span class="ml-1.5 text-stone-400">{count(t.id)}</span>{/if}
    </button>
  {/each}
</nav>

<div class="card mt-4 overflow-hidden" data-tour="reports">
  <table class="w-full text-left text-sm">
    <thead class="hidden bg-stone-50 text-xs uppercase tracking-wider text-stone-500 md:table-header-group">
      <tr>
        <th class="px-4 py-3 font-semibold">Auftrag</th><th class="px-4 py-3 font-semibold">Kunde / Anlage</th>
        <th class="px-4 py-3 font-semibold">Techniker</th><th class="px-4 py-3 text-right font-semibold">Zeit</th>
        <th class="px-4 py-3 text-right font-semibold">Material</th><th class="px-4 py-3 font-semibold">Status</th>
      </tr>
    </thead>
    <tbody class="divide-y divide-stone-100">
      {#each visible as j (j.id)}
        <tr class="block cursor-pointer hover:bg-brand-50/50 md:table-row" data-testid="office-row" data-tour={j.status === 'Completed' ? 'review-row' : undefined}>
          <td class="block px-4 pt-3 md:table-cell md:py-3">
            <a href="/buero/{j.id}" class="font-mono font-medium text-brand-700 hover:underline">{j.number}</a>
            <span class="block text-xs text-stone-500">{fmtDate(j.scheduledFor)}</span>
          </td>
          <td class="block px-4 md:table-cell md:py-3"><span class="font-medium">{j.customer}</span><span class="block text-xs text-stone-500">{j.machine}</span></td>
          <td class="block px-4 text-stone-600 md:table-cell md:py-3">{j.technician}</td>
          <td class="inline-block px-4 tabular-nums md:table-cell md:py-3 md:text-right">{fmtDuration(j.totalMinutes)}</td>
          <td class="inline-block px-4 tabular-nums md:table-cell md:py-3 md:text-right">{fmtMoney(j.materialTotal)}</td>
          <td class="block px-4 pb-3 md:table-cell md:py-3"><StatusChip status={j.status} /></td>
        </tr>
      {:else}
        <tr><td class="px-4 py-10 text-center text-stone-500" colspan="6">Keine Berichte in dieser Ansicht.</td></tr>
      {/each}
    </tbody>
  </table>
</div>
