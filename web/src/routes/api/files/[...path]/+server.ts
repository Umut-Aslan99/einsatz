import { error } from '@sveltejs/kit';
import { upstream } from '$lib/server/upstream';

const ALLOWED = /^(photos\/[0-9a-f-]{36}|jobs\/[0-9a-f-]{36}\/(signature|report\.pdf)|integrations\/[0-9a-f-]{36})$/;

/** Streams photos, signatures, PDFs and export files from the API with the user's rights. */
export const GET = async (event) => {
  if (!ALLOWED.test(event.params.path)) error(404, 'Nicht gefunden.');
  const res = await upstream(event, `/api/files/${event.params.path}`);
  if (!res.ok) error(res.status, 'Datei nicht verfügbar.');
  const headers = new Headers({ 'content-type': res.headers.get('content-type') ?? 'application/octet-stream', 'cache-control': 'private, max-age=300' });
  const disposition = res.headers.get('content-disposition');
  if (disposition) headers.set('content-disposition', disposition.replace(/^attachment/, 'inline'));
  return new Response(res.body, { headers });
};
