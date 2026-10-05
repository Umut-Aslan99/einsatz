import { expect, test, type Page } from '@playwright/test';

async function sign(page: Page) {
  const pad = page.getByTestId('signature-pad');
  await pad.scrollIntoViewIfNeeded();
  const box = (await pad.boundingBox())!;
  await page.mouse.move(box.x + 30, box.y + box.height * 0.6);
  await page.mouse.down();
  for (let i = 1; i <= 12; i++) await page.mouse.move(box.x + 30 + i * 18, box.y + box.height * (0.6 + (i % 2 ? -0.25 : 0.1)));
  await page.mouse.up();
}

test.describe.serial('field report: offline on site → office → ERP', () => {
  let jobNumber = '';

  test.beforeAll(async ({ request, baseURL }) => {
    // Start from a known state – through the BFF's form actions, like a browser would.
    const headers = { origin: baseURL!, accept: 'application/json' };
    expect((await request.post('/login', { form: { login: 'petra', password: 'demo' }, headers })).ok()).toBeTruthy();
    expect((await request.post('/buero?/reset', { form: {}, headers })).ok()).toBeTruthy();
  });

  test('technician completes a report without network, then syncs', async ({ page, context }) => {
    await page.goto('/login');
    await page.getByTestId('demo-jonas').click();
    await expect(page).toHaveURL(/\/t$/);
    await page.evaluate(() => navigator.serviceWorker.ready);

    const card = page.getByTestId('job-card').first();
    jobNumber = (await card.locator('p.font-mono').textContent())!.split(' · ')[0]!.trim();
    await card.click();
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    // ── No network from here on ──
    await context.setOffline(true);
    await expect(page.getByTestId('sync-pill')).toHaveAttribute('data-online', 'false');

    await page.getByTestId('start').click();
    await page.getByTestId('add-material').click();
    await page.getByPlaceholder('Artikelnummer oder Bezeichnung').fill('Dichtung');
    await page.getByRole('button', { name: /Dichtungssatz/ }).click();
    await page.getByRole('button', { name: 'Mehr' }).click();
    await page.getByRole('button', { name: 'Übernehmen', exact: true }).click();
    await expect(page.getByText('Dichtungssatz Zylinder Ø50')).toBeVisible();

    await page.getByTestId('notes').fill('Beide Dichtungen getauscht, Probelauf ohne Befund.');
    await page.getByTestId('notes').blur();
    await page.getByTestId('stop').click();

    await page.getByTestId('signer').fill('M. Hüsing');
    await sign(page);
    await page.getByTestId('sign').click();
    await expect(page.getByText('Unterschrieben von')).toBeVisible();

    await page.getByTestId('complete').click();
    await expect(page.getByText('Zur Prüfung').first()).toBeVisible();
    const pending = Number(await page.getByTestId('sync-pill').getAttribute('data-pending'));
    expect(pending).toBeGreaterThanOrEqual(6);

    // Closing and reopening the app offline loses nothing: shell from the service worker, data from IndexedDB.
    await page.reload();
    await expect(page.getByText('Dichtungssatz Zylinder Ø50')).toBeVisible();
    await expect(page.getByTestId('sync-pill')).toHaveAttribute('data-pending', String(pending));

    // ── Back in range: the outbox drains by itself ──
    await context.setOffline(false);
    await expect(page.getByTestId('sync-pill')).toHaveAttribute('data-pending', '0', { timeout: 30_000 });
    await expect(page.getByTestId('sync-pill')).toHaveAttribute('data-online', 'true');
  });

  test('office approves the report into ERP and DMS', async ({ page }) => {
    await page.goto('/login');
    await page.getByTestId('demo-petra').click();
    await expect(page).toHaveURL(/\/buero$/);

    await page.getByRole('link', { name: jobNumber }).click();
    await expect(page.getByText('M. Hüsing')).toBeVisible();
    await expect(page.getByText('Beide Dichtungen getauscht')).toBeVisible();

    await page.getByTestId('approve').click();
    const files = page.getByTestId('integrations');
    await expect(files).toContainText('ERP');
    await expect(files).toContainText('DMS');
    await expect(page.getByText('Im ERP').first()).toBeVisible();

    await files.getByText('ERP-Beleg (XML) ansehen').click();
    await expect(files.locator('pre')).toContainText('<Artikelnummer>A-10020</Artikelnummer>');
    await expect(files.locator('pre')).toContainText('<Menge>2</Menge>');

    const pdf = await page.request.get(`${new URL(page.url()).origin}/api/files/jobs/${page.url().split('/').pop()}/report.pdf`);
    expect(pdf.headers()['content-type']).toBe('application/pdf');
  });

  test('technician sees the final state and cannot edit anymore', async ({ page }) => {
    await page.goto('/login');
    await page.getByTestId('demo-jonas').click();
    await page.getByRole('heading', { name: 'Erledigt' }).or(page.getByText(/Erledigt ·/)).first().waitFor();
    await page.getByTestId('job-card').filter({ hasText: jobNumber }).click();
    await expect(page.getByText('Bericht freigegeben und an das ERP übergeben.')).toBeVisible();
    await expect(page.getByTestId('start')).toHaveCount(0);
  });
});
