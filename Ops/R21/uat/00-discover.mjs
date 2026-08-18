/** استكشاف بنية الشاشات على TEST قبل كتابة رحلة UAT — لا يكتب أيّ بيانات. */
import { readSecrets, launch, login, goto, instrument, newSink, PERSONAS, BASE } from './lib.mjs';

const s = readSecrets();
const { browser, makeContext } = await launch(s);
const ctx = await makeContext();
const page = await ctx.newPage();
const sink = newSink();
instrument(page, sink);

const landed = await login(page, PERSONAS.ADMIN.email, s.UAT_PW);
console.log('LANDED', landed.replace(BASE, ''));

async function dump(label, url) {
  await goto(page, url, 2500);
  const buttons = await page.$$eval('button', (els) =>
    [...new Set(els.filter((e) => e.offsetParent !== null).map((e) => (e.textContent || '').trim()).filter(Boolean))]);
  const links = await page.$$eval('a[href^="/app"]', (els) =>
    [...new Set(els.map((e) => `${(e.textContent || '').trim()}|${e.getAttribute('href')}`))]).catch(() => []);
  console.log(`\n=== ${label} (${url}) === url=${page.url().replace(BASE, '')}`);
  console.log('BUTTONS:', JSON.stringify(buttons).slice(0, 1200));
  console.log('LINKS:', JSON.stringify(links.slice(0, 25)).slice(0, 1200));
}

await dump('CLIENTS', '/app/clients');
// افتح نموذج إنشاء العميل واسرد حقوله
try {
  const els = await page.$$('button');
  for (const el of els) {
    const t = ((await el.textContent()) || '').trim();
    if (/عميل/.test(t) && /إضافة|جديد|إنشاء/.test(t) && (await el.isVisible())) { await el.click(); break; }
  }
  await page.waitForTimeout(1500);
  const fields = await page.$$eval('input,select,textarea', (els) =>
    els.filter((e) => e.offsetParent !== null).map((e) => ({
      tag: e.tagName, type: e.type, name: e.name, id: e.id,
      ph: e.placeholder, label: (e.closest('label')?.textContent
        || (e.id && document.querySelector(`label[for="${e.id}"]`)?.textContent) || '').trim().slice(0, 40),
      opts: e.tagName === 'SELECT' ? [...e.options].map((o) => `${o.value}|${o.textContent.trim()}`).slice(0, 12) : undefined,
    })));
  console.log('\nCLIENT FORM FIELDS:', JSON.stringify(fields, null, 1).slice(0, 3500));
  const dlgButtons = await page.$$eval('[role=dialog] button, form button', (els) =>
    els.map((e) => (e.textContent || '').trim()).filter(Boolean));
  console.log('DIALOG BUTTONS:', JSON.stringify(dlgButtons));
} catch (e) { console.log('CLIENT FORM ERROR', String(e).slice(0, 200)); }

console.log('\nSINK', JSON.stringify({
  console: sink.console.length, failed: sink.failedRequests.length,
  forbidden: sink.forbiddenHosts.length, http: sink.httpErrors.slice(0, 5),
}));

await browser.close();
