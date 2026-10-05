import { error, fail } from '@sveltejs/kit';
import { approve, detail, returnToTechnician } from '$lib/server/office';

export const load = async (event) => {
  const d = await detail(event, event.params.id);
  if (!d) error(404, 'Bericht nicht gefunden.');
  return d;
};

/** Workflow steps as form actions: progressive enhancement, CSRF-checked by SvelteKit. */
export const actions = {
  approve: async (event) => {
    await approve(event, event.params.id);
    return { done: 'approved' };
  },
  return: async (event) => {
    const reason = String((await event.request.formData()).get('reason') ?? '').trim();
    if (!reason) return fail(400, { error: 'Bitte einen Grund angeben.' });
    await returnToTechnician(event, event.params.id, reason);
    return { done: 'returned' };
  },
};
