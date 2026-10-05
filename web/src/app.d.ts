import type { User } from '$lib/types';

declare global {
  namespace App {
    interface Locals {
      /** Signed-in user, decoded from the BFF session cookie. */
      user: User | null;
      /** The .NET API's auth cookie. Lives only on the server, never in the browser. */
      upstream: string | null;
    }
    interface Error {
      message: string;
    }
  }
}

export {};
