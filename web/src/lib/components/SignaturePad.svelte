<script lang="ts">
  /** Finger/stylus signature on a canvas, exported as PNG. Sized for the device pixel ratio so lines stay crisp. */
  let { onchange }: { onchange?: (empty: boolean) => void } = $props();

  let canvas: HTMLCanvasElement;
  let ctx: CanvasRenderingContext2D;
  let drawing = false;
  let empty = $state(true);
  let last: { x: number; y: number } | null = null;

  $effect(() => {
    const dpr = Math.max(1, devicePixelRatio || 1);
    const rect = canvas.getBoundingClientRect();
    canvas.width = rect.width * dpr;
    canvas.height = rect.height * dpr;
    ctx = canvas.getContext('2d')!;
    ctx.scale(dpr, dpr);
    ctx.lineWidth = 2.4;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.strokeStyle = '#141428';
  });

  const point = (e: PointerEvent) => {
    const r = canvas.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  };

  function down(e: PointerEvent) {
    canvas.setPointerCapture(e.pointerId);
    drawing = true;
    last = point(e);
  }

  function move(e: PointerEvent) {
    if (!drawing || !last) return;
    const p = point(e);
    ctx.beginPath();
    ctx.moveTo(last.x, last.y);
    ctx.lineTo(p.x, p.y);
    ctx.stroke();
    last = p;
    if (empty) { empty = false; onchange?.(false); }
  }

  function up() { drawing = false; last = null; }

  export function clear() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    empty = true;
    onchange?.(true);
  }

  export function toBase64(): string {
    return canvas.toDataURL('image/png').split(',')[1]!;
  }
</script>

<div class="relative">
  <canvas
    bind:this={canvas}
    class="h-44 w-full touch-none rounded-xl border-2 border-dashed border-stone-300 bg-white"
    onpointerdown={down} onpointermove={move} onpointerup={up} onpointercancel={up} onpointerleave={up}
    aria-label="Unterschriftenfeld" data-testid="signature-pad"
  ></canvas>
  {#if empty}<span class="pointer-events-none absolute inset-0 grid place-items-center text-stone-400">Hier unterschreiben</span>{/if}
  <span class="pointer-events-none absolute bottom-8 left-6 right-6 border-b border-stone-300"></span>
</div>
