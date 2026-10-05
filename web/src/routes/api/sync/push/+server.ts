import { upstream } from '$lib/server/upstream';

/** Forwards the device outbox unchanged; idempotency and business rules live in the API. */
export const POST = async (event) => {
  const res = await upstream(event, '/api/sync/push', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: await event.request.text(),
  });
  return new Response(res.body, { status: res.status, headers: { 'content-type': res.headers.get('content-type') ?? 'application/json' } });
};
