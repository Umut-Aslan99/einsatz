<script lang="ts">
  import { statusLabel, type JobStatus } from '$lib/types';

  let { status }: { status: JobStatus } = $props();
  const tone: Record<JobStatus, string> = {
    Planned: 'bg-stone-100 text-stone-600 ring-stone-200',
    InProgress: 'bg-amber-50 text-amber-800 ring-amber-200',
    Completed: 'bg-brand-50 text-brand-700 ring-brand-100',
    Approved: 'bg-sky-50 text-sky-800 ring-sky-200',
    Exported: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  };
</script>

<span class="inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset {tone[status]}">{statusLabel[status]}</span>
