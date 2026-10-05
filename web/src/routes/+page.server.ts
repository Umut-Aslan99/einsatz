import { redirect } from '@sveltejs/kit';

export const load = ({ locals }) => {
  redirect(303, !locals.user ? '/login' : locals.user.role === 'Office' ? '/buero' : '/t');
};
