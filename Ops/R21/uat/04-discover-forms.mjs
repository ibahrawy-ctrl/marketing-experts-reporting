/** استكشاف حقول نماذج البنية (الموجز/الاستراتيجيّة/الهدف/المخرَج/مسار العمل) — لا يحفظ شيئًا. */
import path from 'node:path';
import fs from 'node:fs';
import { readSecrets, launch, login, goto, instrument, newSink, PERSONAS, clickText, EVIDENCE_ROOT } from './lib.mjs';

const { projectId } = JSON.parse(fs.readFileSync(path.join(EVIDENCE_ROOT, 'ids.json'), 'utf8'));
const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
instrument(page, newSink());
await login(page, PERSONAS.ADMIN.email, s.UAT_PW);

const dump = async (tag) => {
  const f = await page.$$eval('input,select,textarea', (els) =>
    els.filter((e) => e.offsetParent !== null).map((e, i) => ({
      i, t: e.tagName, ty: e.type,
      lb: (e.closest('label')?.textContent || '').trim().slice(0, 40),
      ph: e.placeholder || undefined,
      o: e.tagName === 'SELECT' ? [...e.options].map((x) => `${x.value}|${x.textContent.trim()}`).slice(0, 10) : undefined,
    })));
  const b = await page.$$eval('button', (els) =>
    [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
  console.log(`\n### ${tag}\nFIELDS ${JSON.stringify(f).slice(0, 2600)}\nBUTTONS ${JSON.stringify(b.slice(-12))}`);
};

const tab = async (name) => { await clickText(page, 'button', new RegExp(`^${name}$`)); await page.waitForTimeout(1500); };

await goto(page, `/app/projects/${projectId}/360`, 3500);
await tab('الموجز والسياق'); await dump('BRIEF');
await tab('الاستراتيجيّة');
try { await clickText(page, 'button', /تسجيل الاستراتيجيّة/); await page.waitForTimeout(1200); } catch {}
await dump('STRATEGY');
await goto(page, `/app/projects/${projectId}/360`, 3000);
await tab('الأهداف');
try { await clickText(page, 'button', /هدف جديد/); await page.waitForTimeout(1200); } catch {}
await dump('OBJECTIVE');
await goto(page, `/app/projects/${projectId}/360`, 3000);
await tab('المخرَجات التعاقديّة');
try { await clickText(page, 'button', /مخرَج تعاقديّ جديد/); await page.waitForTimeout(1200); } catch {}
await dump('DELIVERABLE');
await goto(page, `/app/projects/${projectId}`, 3000);
await dump('PROJECT-DETAIL');
await browser.close();
