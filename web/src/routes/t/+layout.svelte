<script lang="ts">
  import { untrack } from 'svelte';
  import Logo from '$lib/components/Logo.svelte';
  import SyncPill from '$lib/components/SyncPill.svelte';
  import { field } from '$lib/offline/field.svelte';
  import { session } from '$lib/session.svelte';

  let { children } = $props();

  $effect(() => {
    untrack(() => session.load());
  });

  $effect(() => {
    if (!session.ready) return;
    if (!session.user || session.user.role !== 'Technician') { location.href = '/login'; return; }
    const user = session.user;
    untrack(() => field.start(user));
    return () => field.stop();
  });

  async function logout() {
    if (field.pendingCount > 0 && !confirm(`${field.pendingCount} Änderungen sind noch nicht übertragen und gehen beim Abmelden verloren. Trotzdem abmelden?`)) return;
    field.stop();
    await session.logout();
    location.href = '/login';
  }
</script>

<header class="sticky top-0 z-20 bg-ink text-white">
  <div class="mx-auto flex h-14 max-w-3xl items-center gap-3 px-4">
    <a href="/t"><Logo dark /></a>
    <span class="ml-auto"><SyncPill /></span>
    <button class="text-sm text-stone-400 hover:text-white" onclick={logout}>Abmelden</button>
  </div>
  <div class="h-1 w-2/5 bg-brand-500"></div>
</header>

{#if field.sessionExpired}
  <div class="bg-red-600 px-4 py-3 text-center text-sm text-white">
    Sitzung abgelaufen – bitte <a class="underline" href="/login">neu anmelden</a>. Ihre {field.pendingCount} offenen Änderungen bleiben gespeichert.
  </div>
{/if}

{#each field.rejected as r (r.command.id)}
  <div class="mx-auto mt-3 flex max-w-3xl items-start gap-3 rounded-xl bg-red-50 p-3 text-sm text-red-800 ring-1 ring-red-200" role="alert">
    <span class="flex-1"><strong>Nicht übernommen:</strong> {r.error}</span>
    <button class="font-semibold" onclick={() => field.dismissRejected(r.command.id)}>OK</button>
  </div>
{/each}

<main class="mx-auto max-w-3xl px-4 pb-28 pt-5">
  {#if session.ready && session.user}{@render children()}{:else}<p class="text-stone-400">Lädt …</p>{/if}
</main>
