import { test, expect } from '@playwright/test';

test('landing page shows system title and login CTA', async ({ page }) => {
  await page.goto('/');
  await expect(
    page.getByText('نظام تقارير الأداء والتشغيل الداخلي'),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'تسجيل الدخول' })).toBeVisible();
});
