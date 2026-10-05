<script lang="ts">
  import StatusChip from '$lib/components/StatusChip.svelte';
  import { field } from '$lib/offline/field.svelte';
  import { fmtDate, todayIso } from '$lib/format';
  import { session } from '$lib/session.svelte';
  import type { Job } from '$lib/types';

  const today = todayIso();
  const open = (j: Job) => j.status === 'Planned' || j.status === 'InProgress';
  const groups = $derived([
    { title: 'Heute', jobs: field.jobs.filter((j) => j.scheduledFor === today && open(j)) },
    { title: 'Liegengeblieben', jobs: field.jobs.filter((j) => j.scheduledFor < today && open(j)) },
    { title: 'Demnächst', jobs: field.jobs.filter((j) => j.scheduledFor > today && open(j)) },
    { title: 'Erledigt', jobs: field.jobs.filter((j) => !open(j)).toReversed() },
  ].filter((g) => g.jobs.length > 0));
</script>

<svelte:head><title>Meine Einsätze · Einsatz</title></svelte:head>

<h1 class="text-2xl font-bold">Moin, {session.user?.name.split(' ')[0]}.</h1>
<p class="text-stone-600">
  {new Date().toLocaleDateString('de-DE', { weekday: 'long', day: 'numeric', month: 'long' })}
  {#if !field.online}· <strong class="text-amber-700">offline – alles wird lokal gespeichert</strong>{/if}
</p>

{#if field.jobs.length === 0}
  <p class="card mt-6 p-6 text-stone-500">{field.syncing ? 'Aufträge werden geladen …' : 'Keine Aufträge vorhanden.'}</p>
{/if}

{#each groups as g (g.title)}
  <h2 class="label mt-8">{g.title} · {g.jobs.length}</h2>
  <ul class="mt-3 grid gap-3">
    {#each g.jobs as j (j.id)}
      <li>
        <a href="/t/{j.id}" class="card block p-4 transition hover:border-brand-500 active:scale-[0.99]" data-testid="job-card">
          <div class="flex items-start justify-between gap-3">
            <div class="min-w-0">
              <p class="font-mono text-xs text-stone-500">{j.number} · {fmtDate(j.scheduledFor)}</p>
              <p class="mt-1 truncate text-lg font-semibold">{j.customer.name}</p>
              <p class="truncate text-sm text-stone-600">{j.machine.name} · {j.customer.city}</p>
            </div>
            <StatusChip status={j.status} />
          </div>
          <p class="mt-3 line-clamp-2 text-sm text-stone-700">{j.task}</p>
          {#if j.returnReason}<p class="mt-2 text-sm font-medium text-red-700">Zurück vom Innendienst: {j.returnReason}</p>{/if}
        </a>
      </li>
    {/each}
  </ul>
{/each}
