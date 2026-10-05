/// <reference types="@sveltejs/kit" />
/// <reference no-default-lib="true"/>
/// <reference lib="esnext" />
/// <reference lib="webworker" />
import { build, files, version } from '$service-worker';

// Only the technician app works offline: its client-rendered shell (/t) and assets are precached,
// its data lives in IndexedDB. Office pages are rendered by the BFF and simply need a connection.
const sw = self as unknown as ServiceWorkerGlobalScope;
const SHELL = `shell-${version}`;
const MEDIA = 'media-v1';
const FIELD_SHELL = '/t';
const PRECACHE = [FIELD_SHELL, ...build, ...files];

sw.addEventListener('install', (event) => {
  event.waitUntil(caches.open(SHELL).then((c) => c.addAll(PRECACHE)).then(() => sw.skipWaiting()));
});

sw.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    for (const key of await caches.keys()) if (key !== SHELL && key !== MEDIA) await caches.delete(key);
    await sw.clients.claim();
  })());
});

sw.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== location.origin) return;

  // Photos and signatures never change once uploaded: cache-first so they stay visible offline.
  if (url.pathname.startsWith('/api/files/photos/')) {
    event.respondWith(caches.open(MEDIA).then(async (c) => {
      const hit = await c.match(req);
      if (hit) return hit;
      const res = await fetch(req);
      if (res.ok) c.put(req, res.clone());
      return res;
    }));
    return;
  }
  if (url.pathname.startsWith('/api/') || url.pathname === '/logout') return;

  // Navigation: network first (fresh deploys, server-rendered office). Offline, every /t/… URL gets the
  // cached field-app shell; the client router then renders the right page from IndexedDB.
  if (req.mode === 'navigate') {
    event.respondWith(fetch(req).catch(async () => {
      const shell = url.pathname === FIELD_SHELL || url.pathname.startsWith(`${FIELD_SHELL}/`) ? await caches.match(FIELD_SHELL) : undefined;
      return shell ?? new Response('<!doctype html><meta charset="utf-8"><title>Offline</title><p style="font:16px system-ui;padding:2rem">Keine Verbindung. Die Techniker-App unter <a href="/t">/t</a> funktioniert auch offline.</p>',
        { headers: { 'content-type': 'text/html; charset=utf-8' } });
    }));
    return;
  }
  event.respondWith(caches.match(req).then((hit) => hit ?? fetch(req)));
});
