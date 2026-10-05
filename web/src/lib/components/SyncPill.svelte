<script lang="ts">
  import { field } from '$lib/offline/field.svelte';
  import { fmtTime } from '$lib/format';

  const label = $derived(
    !field.online ? 'Offline' : field.syncing ? 'Synchronisiert …' : field.pendingCount > 0 ? 'Wartet' : 'Aktuell');
</script>

<button
  class="inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-sm font-medium ring-1 ring-inset transition
    {field.online ? 'bg-white/10 text-white ring-white/20' : 'bg-amber-400 text-ink ring-amber-500'}"
  onclick={() => field.sync()}
  title={field.lastSync ? `Zuletzt synchronisiert ${fmtTime(field.lastSync)}` : 'Noch nie synchronisiert'}
  data-testid="sync-pill"
  data-online={field.online}
  data-pending={field.pendingCount}
>
  <span class="size-2 rounded-full {field.online ? (field.pendingCount ? 'bg-amber-400' : 'bg-emerald-400') : 'bg-ink'}
    {field.syncing ? 'animate-pulse' : ''}"></span>
  {label}
  {#if field.pendingCount > 0}<span class="rounded-full bg-black/20 px-1.5 text-xs tabular-nums">{field.pendingCount}</span>{/if}
</button>
