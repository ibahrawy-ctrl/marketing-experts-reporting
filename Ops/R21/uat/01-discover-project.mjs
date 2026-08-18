/** استكشاف نموذج إنشاء المشروع وتبويبات Project 360 — لا يكتب بيانات. */
import { readSecrets, launch, login, goto, instrument, newSink, PERSONAS, BASE } from './lib.mjs';

const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
instrument(page, newSink());
await login(page, PERSONAS.ADMIN.email, s.UAT_PW);

const dumpFields = async (tag) => {
  const fields = await page.$$eval('input,select,textarea', (els) =>
    els.filter((e) => e.offsetParent !== null).map((e) => ({
      t: e.tagName, ty: e.type,
      lb: (e.closest('label')?.textContent || '').trim().slice(0, 45),
      ph: e.placeholder || undefined,
      o: e.tagName === 'SELECT' ? [...e.options].map((x) => `${x.value}|${x.textContent.trim()}`).slice(0, 14) : undefined,
    })));
  console.log(`\n### ${tag} FIELDS\n` + JSON.stringify(fields).slice(0, 4000));
  const btns = await page.$$eval('button', (els) =>
    [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
  console.log(`### ${tag} BUTTONS\n` + JSON.stringify(btns).slice(0, 1500));
};

await goto(page, '/app/projects', 2500);
await dumpFields('PROJECTS-LIST');
for (const el of await page.$$('button')) {
  const t = ((await el.textContent()) || '').trim();
  if (/مشروع/.test(t) && /إضافة|جديد|إنشاء/.test(t) && (await el.isVisible())) { await el.click(); break; }
}
await page.waitForTimeout(1500);
await dumpFields('PROJECT-CREATE');

await browser.close();
