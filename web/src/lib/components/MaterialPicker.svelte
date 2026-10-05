<script lang="ts">
  import { fmtMoney } from '$lib/format';
  import type { Article } from '$lib/types';

  let { articles, onpick, onclose }: { articles: Article[]; onpick: (a: Article, qty: number) => void; onclose: () => void } = $props();

  let query = $state('');
  let selected = $state<Article | null>(null);
  let qty = $state(1);
  let dialog: HTMLDialogElement;

  const hits = $derived(articles.filter((a) =>
    `${a.number} ${a.name}`.toLowerCase().includes(query.trim().toLowerCase())).slice(0, 30));

  $effect(() => { dialog.showModal(); });
</script>

<dialog bind:this={dialog} onclose={onclose}
  class="m-0 mt-auto w-full max-w-none rounded-t-3xl p-0 backdrop:bg-black/50 sm:m-auto sm:max-w-lg sm:rounded-3xl">
  <div class="flex max-h-[85dvh] flex-col">
    <div class="border-b border-stone-200 p-4">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-bold">Material hinzufügen</h2>
        <button class="text-stone-500" onclick={() => dialog.close()}>Schließen</button>
      </div>
      <!-- svelte-ignore a11y_autofocus -->
      <input class="input mt-3" placeholder="Artikelnummer oder Bezeichnung" bind:value={query} autofocus />
    </div>

    {#if selected}
      <div class="p-4">
        <p class="font-mono text-xs text-stone-500">{selected.number}</p>
        <p class="text-lg font-semibold">{selected.name}</p>
        <p class="text-sm text-stone-500">{fmtMoney(selected.price)} / {selected.unit}</p>
        <div class="mt-5 flex items-center justify-center gap-4">
          <button class="btn-ghost size-14 text-2xl" onclick={() => (qty = Math.max(0.5, qty - 1))} aria-label="Weniger">−</button>
          <input class="input w-28 text-center text-2xl font-bold tabular-nums" type="number" min="0.5" step="0.5" bind:value={qty} aria-label="Menge" />
          <button class="btn-ghost size-14 text-2xl" onclick={() => (qty += 1)} aria-label="Mehr">+</button>
        </div>
        <div class="mt-6 grid grid-cols-2 gap-3">
          <button class="btn-ghost" onclick={() => (selected = null)}>Zurück</button>
          <button class="btn-primary" disabled={!(qty > 0)} onclick={() => { onpick(selected!, qty); dialog.close(); }}>Übernehmen</button>
        </div>
      </div>
    {:else}
      <ul class="flex-1 overflow-y-auto p-2">
        {#each hits as a (a.number)}
          <li>
            <button class="flex w-full items-center gap-3 rounded-xl p-3 text-left hover:bg-stone-100" onclick={() => { selected = a; qty = 1; }}>
              <span class="w-20 shrink-0 font-mono text-xs text-stone-500">{a.number}</span>
              <span class="flex-1">{a.name}</span>
              <span class="text-sm text-stone-500">{a.unit}</span>
            </button>
          </li>
        {:else}
          <li class="p-6 text-center text-stone-500">Kein Artikel gefunden.</li>
        {/each}
      </ul>
    {/if}
  </div>
</dialog>
