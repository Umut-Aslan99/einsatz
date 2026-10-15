import { expect, test, type Page } from '@playwright/test';

// The guided tour (site-tour.js): starts on the sign-in page and continues in the app of the chosen role.
// Every step must find its element – a renamed or removed part of the UI fails here. The tour only looks, never changes data.
async function tourAs(page: Page, login: string): Promise<string[]> {
  const skipped: string[] = [];
  page.on('console', (m) => { if (m.text().includes('[site-tour]')) skipped.push(m.text()); });
  await page.goto('/login?tour=start');
  const tour = page.locator('[data-site-tour="einsatz"]');
  await expect(tour.locator('h2')).toHaveText('Hallo! Kurz zum Problem');
  await tour.getByTestId('tour-next').click();
  await expect(tour.locator('h2')).toHaveText('Wählen Sie eine Rolle');
  await page.getByTestId(`demo-${login}`).click();

  const titles: string[] = [];
  while (titles.length < 30) {
    const bubble = tour.locator('section.bubble');
    await expect(bubble).toBeVisible({ timeout: 15_000 });
    await expect(bubble).not.toHaveAttribute('data-step', '2', { timeout: 15_000 });
    const step = await bubble.getAttribute('data-step');
    titles.push((await tour.locator('h2').textContent()) ?? '');
    await tour.getByTestId('tour-next').click();
    if ((await tour.count()) === 0) break;
    await expect(bubble).not.toHaveAttribute('data-step', step!, { timeout: 15_000 });
  }
  expect(skipped).toEqual([]);
  expect(await page.evaluate(() => localStorage.getItem('site-tour:einsatz'))).toBe('done');
  return titles;
}

test('the technician tour shows the tablet app from job list to signature', async ({ page }) => {
  const titles = await tourAs(page, 'jonas');
  expect(titles).toEqual(['Meine Einsätze', 'Kein Empfang? Kein Problem', 'Arbeitszeit per Knopfdruck', 'Material aus dem Artikelstamm',
    'Fotos direkt vom Tablet', 'Unterschrift beim Kunden', 'Warum das hilft']);
});

test('the back office tour shows review and hand-over', async ({ page }) => {
  const titles = await tourAs(page, 'petra');
  expect(titles.slice(0, 2)).toEqual(['Der Innendienst', 'Berichte prüfen']);
  expect(titles.at(-1)).toBe('Warum das hilft');
});
