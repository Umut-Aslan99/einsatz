import { json } from '@sveltejs/kit';
import { upstream } from '$lib/server/upstream';
import type { Job } from '$lib/types';

/** Snapshot for the device, trimmed to what the field app uses (no ERP/DMS internals). */
export const GET = async (event) => {
  const res = await upstream(event, '/api/sync/pull');
  if (!res.ok) return new Response(res.body, { status: res.status, headers: { 'content-type': 'application/json' } });
  const body = (await res.json()) as { serverTime: string; jobs: Job[]; articles: unknown[] };
  return json({
    ...body,
    jobs: body.jobs.map(({ integrations: _i, approvedBy: _a, ...job }) => ({ ...job, integrations: [], approvedBy: null })),
  });
};
