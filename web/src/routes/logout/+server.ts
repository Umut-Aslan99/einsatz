import { redirect } from '@sveltejs/kit';
import { clearSession } from '$lib/server/session';
import { upstream } from '$lib/server/upstream';

export const POST = async (event) => {
  if (event.locals.upstream) await upstream(event, '/api/auth/logout', { method: 'POST' }).catch(() => {});
  clearSession(event.cookies);
  // Forms get a redirect; the technician app calls this via fetch and handles navigation itself.
  if (event.request.headers.get('accept')?.includes('application/json')) return new Response(null, { status: 204 });
  redirect(303, '/login');
};
