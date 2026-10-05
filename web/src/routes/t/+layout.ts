// The field app renders in the browser only: it must start from the service-worker cache without network,
// with data from IndexedDB. Hence no server load functions in this subtree.
export const ssr = false;
