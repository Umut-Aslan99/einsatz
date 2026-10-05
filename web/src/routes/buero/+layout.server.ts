import { redirect } from '@sveltejs/kit';

/** Office area: rendered on the BFF, guarded by the server-side session. */
export const load = ({ locals }) => {
  if (locals.user?.role !== 'Office') redirect(303, '/login');
  return { user: locals.user };
};
