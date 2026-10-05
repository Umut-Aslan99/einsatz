<script lang="ts">
  let { dark = false }: { dark?: boolean } = $props();
</script>

<span class="inline-flex items-center gap-2 font-bold tracking-tight {dark ? 'text-white' : 'text-ink'}">
  <svg viewBox="0 0 32 32" class="size-7" aria-hidden="true">
    <rect width="32" height="32" rx="8" fill="#111214" />
    <path d="M9 9h13M9 16h9M9 23h13" stroke="#f4f1ea" stroke-width="3" stroke-linecap="round" />
    <rect x="9" y="26" width="10" height="2.5" rx="1" fill="#e8834a" />
  </svg>
  Einsatz
</span>
