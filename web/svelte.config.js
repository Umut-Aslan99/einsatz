import adapter from '@sveltejs/adapter-node';

/**
 * SvelteKit runs as the Backend-for-Frontend (BFF):
 *   browser ⇄ SvelteKit (session, view models, SSR, form actions) ⇄ .NET API (internal only)
 */
export default {
  kit: {
    adapter: adapter({ precompress: true }),
    serviceWorker: { register: true },
    // Absolute asset URLs: the cached /t shell is served for every /t/… URL while offline.
    paths: { relative: false },
    // SvelteKit hashes its own inline scripts, so no 'unsafe-inline' is needed for scripts.
    csp: {
      mode: 'auto',
      directives: {
        'default-src': ['self'],
        'script-src': ['self'],
        'style-src': ['self', 'unsafe-inline'],
        'img-src': ['self', 'data:', 'blob:'],
        'connect-src': ['self'],
        'worker-src': ['self'],
        'manifest-src': ['self'],
        'frame-ancestors': ['none'],
        'base-uri': ['self'],
        'form-action': ['self'],
      },
    },
  },
};
