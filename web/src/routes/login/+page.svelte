<script lang="ts">
  import { enhance } from '$app/forms';
  import Logo from '$lib/components/Logo.svelte';

  // Login is a form action on the BFF: works without JavaScript, the API credential never reaches the browser.
  let { form } = $props();
  let busy = $state(false);
  const submit = () => {
    busy = true;
    return async ({ update }: { update: () => Promise<void> }) => { await update(); busy = false; };
  };

  const demo = [
    { login: 'jonas', name: 'Jonas Terhaar', role: 'Techniker', hint: 'Tablet-App vor Ort – auch offline' },
    { login: 'lea', name: 'Lea Wissing', role: 'Technikerin', hint: 'Zweiter Außendienst-Account' },
    { login: 'petra', name: 'Petra Lammers', role: 'Innendienst', hint: 'Berichte prüfen, freigeben, ins ERP' },
  ];

</script>

<svelte:head><title>Anmelden · Einsatz</title></svelte:head>

<main class="grid min-h-dvh lg:grid-cols-[1.1fr_1fr]">
  <section class="relative hidden overflow-hidden bg-ink p-12 text-stone-200 lg:flex lg:flex-col lg:justify-between">
    <Logo dark />
    <div class="max-w-lg">
      <p class="font-mono text-xs uppercase tracking-widest text-brand-500">Demo · Serviceberichte</p>
      <h1 class="mt-4 text-4xl font-bold leading-tight text-white">Vom Einsatz vor Ort bis ins ERP – ohne Zettel, auch ohne Netz.</h1>
      <ul class="mt-8 space-y-3 text-stone-300">
        <li>① Techniker erfasst Zeit, Material, Fotos und Unterschrift auf dem Tablet.</li>
        <li>② Kein Empfang in der Halle? Alles bleibt auf dem Gerät und synchronisiert später.</li>
        <li>③ Innendienst prüft, gibt frei – Beleg landet im ERP, PDF im DMS.</li>
      </ul>
      <p class="mt-8 text-sm text-stone-400">
        SvelteKit (Frontend + BFF) · C# ASP.NET Core · EF Core · PostgreSQL · GraphQL · Tailwind · Docker
      </p>
    </div>
    <div class="absolute bottom-0 left-0 h-2 w-2/5 bg-brand-500"></div>
    <p class="text-xs text-stone-500">
      Ein Projekt von <a class="underline hover:text-white" href="https://umut-aslan.dev">Umut Aslan</a> · alle Firmen und Personen sind frei erfunden
    </p>
  </section>

  <section class="flex flex-col justify-center px-5 py-10 sm:px-10">
    <div class="mx-auto w-full max-w-md">
      <div class="lg:hidden"><Logo /></div>
      <h2 class="mt-6 text-2xl font-bold lg:mt-0">Demo starten</h2>
      <p class="mt-1 text-stone-600">Ein Klick genügt – jede Rolle zeigt eine andere Seite des Prozesses.</p>

      <div class="mt-6 grid gap-3">
        {#each demo as d (d.login)}
          <form method="POST" use:enhance={submit}>
          <input type="hidden" name="login" value={d.login} /><input type="hidden" name="password" value="demo" />
          <button class="card flex w-full items-center gap-4 p-4 text-left transition hover:border-brand-500 hover:shadow-md disabled:opacity-50"
            disabled={busy} data-testid="demo-{d.login}">
            <span class="grid size-11 shrink-0 place-items-center rounded-full bg-stone-100 font-bold text-stone-700">{d.name[0]}</span>
            <span class="min-w-0 flex-1">
              <span class="block font-semibold">{d.name} <span class="font-normal text-stone-500">· {d.role}</span></span>
              <span class="block truncate text-sm text-stone-500">{d.hint}</span>
            </span>
            <span class="text-brand-600" aria-hidden="true">→</span>
          </button>
          </form>
        {/each}
      </div>

      <details class="mt-8 text-sm">
        <summary class="cursor-pointer text-stone-500">Manuell anmelden</summary>
        <form class="mt-4 grid gap-3" method="POST" use:enhance={submit}>
          <input class="input" name="login" placeholder="Benutzername" autocomplete="username" value={form?.login ?? ''} required />
          <input class="input" name="password" type="password" placeholder="Passwort (demo)" autocomplete="current-password" required />
          <button class="btn-dark" disabled={busy}>Anmelden</button>
        </form>
      </details>

      {#if form?.error}<p class="mt-4 rounded-xl bg-red-50 p-3 text-sm text-red-700" role="alert">{form.error}</p>{/if}

      <div class="mt-10 rounded-2xl border border-dashed border-stone-300 p-4 text-sm text-stone-600">
        <strong class="text-ink">Offline ausprobieren:</strong> Als Techniker anmelden, dann in den Entwicklertools
        „Offline“ wählen oder am Handy den Flugmodus einschalten. Bericht ausfüllen, wieder online gehen – die Änderungen
        werden automatisch übertragen.
      </div>
    </div>
  </section>
</main>
