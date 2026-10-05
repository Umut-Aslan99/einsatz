import { redirect } from '@sveltejs/kit';
import { listRows, resetDemo } from '$lib/server/office';

export const load = async (event) => ({ rows: await listRows(event) });

export const actions = {
  reset: async (event) => {
    await resetDemo(event);
    redirect(303, '/buero');
  },
};
