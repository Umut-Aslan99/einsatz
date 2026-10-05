<script lang="ts">
  import { enhance } from '$app/forms';
  import Logo from '$lib/components/Logo.svelte';

  let { data, children } = $props();
</script>

<header class="sticky top-0 z-20 bg-ink text-white">
  <div class="mx-auto flex h-14 max-w-6xl items-center gap-4 px-4">
    <a href="/buero"><Logo dark /></a>
    <span class="hidden rounded-full bg-white/10 px-2.5 py-1 text-xs font-medium sm:inline">Innendienst</span>
    <span class="ml-auto hidden text-sm text-stone-400 sm:inline">{data.user.name}</span>
    <form method="POST" action="/buero?/reset" use:enhance={({ cancel }) => {
      if (!confirm('Alle Demo-Daten auf den Ausgangszustand zurücksetzen?')) cancel();
    }}>
      <button class="text-sm text-stone-400 hover:text-white">Demo zurücksetzen</button>
    </form>
    <form method="POST" action="/logout"><button class="text-sm text-stone-400 hover:text-white">Abmelden</button></form>
  </div>
  <div class="h-1 w-2/5 bg-brand-500"></div>
</header>

<main class="mx-auto max-w-6xl px-4 pb-16 pt-6">
  {@render children()}
</main>
