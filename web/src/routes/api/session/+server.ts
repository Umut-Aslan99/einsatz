import { error, json } from '@sveltejs/kit';

/** The technician app asks who is signed in (and caches the answer for offline starts). */
export const GET = ({ locals }) => {
  if (!locals.user) error(401, 'Nicht angemeldet.');
  return json(locals.user);
};
