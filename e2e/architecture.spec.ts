import { expect, test } from '@playwright/test';

test.describe('Backend-for-Frontend boundaries', () => {
  test('the internal API is not reachable through the public origin', async ({ request }) => {
    expect((await request.post('/graphql', { data: { query: '{ jobs { id } }' } })).status()).toBe(404);
    expect((await request.get('/api/openapi/v1.json')).status()).toBe(404);
  });

  test('the session cookie is HttpOnly and the API credential never reaches the browser', async ({ page, context }) => {
    await page.goto('/login');
    await page.getByTestId('demo-petra').click();
    await expect(page).toHaveURL(/\/buero$/);
    const cookies = await context.cookies();
    expect(cookies.map((c) => c.name)).toEqual(['einsatz_session']);
    expect(cookies[0]!.httpOnly).toBe(true);
    expect(cookies[0]!.sameSite).toBe('Strict');
    expect(await page.evaluate(() => document.cookie)).toBe('');
  });

  test('foreign-origin form posts are refused (CSRF)', async ({ request }) => {
    const res = await request.post('/login', { form: { login: 'petra', password: 'demo' }, headers: { origin: 'https://evil.example' } });
    expect(res.status()).toBe(403);
  });
});

test.describe('progressive enhancement', () => {
  test.use({ javaScriptEnabled: false });

  test('office can sign in and review reports without JavaScript', async ({ page }) => {
    await page.goto('/login');
    await page.getByTestId('demo-petra').click();
    await expect(page).toHaveURL(/\/buero$/);
    await expect(page.getByRole('heading', { name: 'Serviceberichte' })).toBeVisible();
    await expect(page.getByTestId('office-row').first()).toBeVisible();
  });
});
