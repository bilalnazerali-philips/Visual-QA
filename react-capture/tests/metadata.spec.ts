import { expect, test } from '@playwright/test';

test('PatientInfo capture contract exposes normalized data-qa metadata', async ({ page }) => {
  await page.setViewportSize({ width: 300, height: 72 });
  await page.setContent(`<div data-qa-root style="width:300px;height:72px;display:flex"><span data-qa="patient-avatar" style="width:48px;height:48px;margin:12px 8px 12px 16px">AB</span><span data-qa="patient-name" style="align-self:center;font-size:16px;font-weight:600">Avery Brooks</span></div>`);
  const item = await page.locator('[data-qa="patient-avatar"]').evaluate(el => { const r = el.getBoundingClientRect(), s = getComputedStyle(el); return { id: el.getAttribute('data-qa'), x: r.x, y: r.y, width: r.width, height: r.height, fontSize: Number.parseFloat(s.fontSize) }; });
  expect(item).toMatchObject({ id: 'patient-avatar', x: 16, y: 12, width: 48, height: 48 });
});
