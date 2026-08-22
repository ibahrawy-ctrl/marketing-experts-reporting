// PROJECT360-R21 — RC multi-role screenshotted UAT (isolated browser on the real rc-report origin).
// Serves the DEPLOYED dist bytes locally; /api and /hubs go to the real RC server (JWT authorizes).
// NEVER logs ws.url() (it carries a JWT). Shared UAT password read from stdin, never printed.
import { chromium } from 'playwright-core';
import fs from 'node:fs';
import path from 'node:path';

const ORIGIN = 'https://rc-report.emarketingacademy.net';
const DIST = path.resolve('/tmp/r21-rc-work/dist-rc');
const OUT = path.resolve(process.argv[2] || '.');
const IDS = JSON.parse(fs.readFileSync('/tmp/r21-rc-work/fixture-ids.json', 'utf8'));
const HOST = new URL(ORIGIN).host;
const UAT_PW = fs.readFileSync(0, 'utf8').trim();   // from stdin, never printed
if (!UAT_PW) { console.log('NO_PW'); process.exit(2); }

const PROJ = IDS.projectId, CLIENT = IDS.clientId, DEL_A = IDS.deliverableA;
const EMAIL = (k) => `rc-uat-${{tl:'tl',owner:'owner',am:'am',empIn:'emp',empOut:'emp-out',mgr:'mgr',gm:'gm',ceo:'ceo',hr:'hr',finmgr:'finmgr',finemp:'finemp',viewer:'viewer'}[k]}@rc-uat.local`;

const MIME = {'.html':'text/html; charset=utf-8','.js':'text/javascript','.css':'text/css','.png':'image/png','.svg':'image/svg+xml','.ico':'image/x-icon','.json':'application/json','.woff2':'font/woff2','.woff':'font/woff','.map':'application/json','.webmanifest':'application/manifest+json'};

const browser = await chromium.launch({ headless: true, executablePath: process.env.PW_CHROME });
const figures = [];
const roleGate = {};   // per-role console/failed
const authMatrix = []; // {actor, action, method, path, status}

function classifyErr(t){ // true => genuine (not teardown/SignalR/designed-negative noise)
  const s = (t||'').toLowerCase();
  // SignalR/websocket teardown + transient negotiation churn (negotiate array proves 200s)
  if (s.includes('signalr')||s.includes('websocket')||s.includes('err_aborted')
      ||s.includes('the connection was stopped')
      ||s.includes('failed to complete negotiation')
      ||s.includes('failed to start the connection')
      ||(s.includes('failed to fetch')&&s.includes('negotiate'))) return false;
  // authorization-boundary responses surface as resource-load 4xx (documented separately in http4xx):
  //  - 404: designed out-of-scope negatives (empOut/viewer overview)
  //  - 403: designed forbidden action (empIn self-review) + RBAC panel boundary (mgr governance-summary)
  if (s.includes('failed to load resource')&&(s.includes('404')||s.includes('403'))) return false;
  return true;
}

async function makeCtx(){
  const ctx = await browser.newContext({ viewport:{width:1440,height:1000}, locale:'ar' });
  const consoleErr=[], failed=[], negot=[], http4xx=[]; let wsCount=0;
  await ctx.route('**/*', async (route)=>{
    const u = new URL(route.request().url());
    if (u.host!==HOST) return route.continue();
    const p=u.pathname;
    if (p.startsWith('/api/')||p.startsWith('/hubs/')||p==='/health') return route.continue();
    let rel = p==='/'?'/index.html':p; let file=path.join(DIST,rel); let ext=path.extname(file).toLowerCase();
    if(!ext||!fs.existsSync(file)){ file=path.join(DIST,'index.html'); ext='.html'; }
    if(!fs.existsSync(file)) return route.fulfill({status:404,body:'x'});
    return route.fulfill({status:200,headers:{'content-type':MIME[ext]||'application/octet-stream'},body:fs.readFileSync(file)});
  });
  const page = await ctx.newPage();
  page.on('console',(m)=>{ if(m.type()==='error') consoleErr.push(m.text()); });
  page.on('pageerror',(e)=>consoleErr.push('PAGEERROR:'+e.message));
  page.on('requestfailed',(r)=>failed.push((r.url().split('?')[0])+' :: '+(r.failure()?.errorText||'')));
  page.on('response',(r)=>{ const u=r.url(); if(u.includes('/hubs/')&&u.includes('negotiate')) negot.push(r.status()); const st=r.status(); if(st>=400&&st<500&&u.includes('/api/')) http4xx.push(st+' '+(u.split('?')[0].replace(ORIGIN,''))); });
  page.on('websocket',()=>{ wsCount++; });  // count only — never read .url()
  return { ctx, page, consoleErr, failed, negot, http4xx, get wsCount(){return wsCount;} };
}

async function login(page, email){
  await page.goto(ORIGIN+'/login',{waitUntil:'domcontentloaded'});
  await page.locator('input[type=email]').fill(email);
  await page.locator('input[type=password]').fill(UAT_PW);
  await Promise.all([
    page.waitForResponse(r=>r.url().includes('/api/auth/login')).catch(()=>{}),
    page.getByRole('button',{name:'دخول'}).click(),
  ]);
  await page.waitForURL('**/app**',{timeout:30000}).catch(()=>{});
  await page.waitForTimeout(1200);
}

async function snap(page, id, caption){
  const f = path.join(OUT, id+'.png');
  await page.screenshot({ path:f, fullPage:false });
  figures.push({ id, caption });
}

async function clickTab(page, label){
  try{ await page.getByRole('tab',{name:label}).click({timeout:4000}); }
  catch{ try{ await page.getByRole('button',{name:label}).click({timeout:4000}); } catch{ await page.getByText(label,{exact:true}).first().click({timeout:4000}).catch(()=>{}); } }
  await page.waitForTimeout(1500);
}

// authenticated fetch inside the page using the stored JWT (proves resource-based auth codes)
async function apiCall(page, method, apiPath, body){
  return await page.evaluate(async ({method, apiPath, body})=>{
    const tok = localStorage.getItem('me_access');
    const res = await fetch('/api'+apiPath, {
      method,
      headers: { 'Content-Type':'application/json', ...(tok?{Authorization:'Bearer '+tok}:{}) },
      body: body!=null?JSON.stringify(body):undefined,
    });
    let data=null; try{ data=await res.json(); }catch{}
    return { status: res.status, data };
  }, {method, apiPath, body});
}

function rec(actor, action, method, p, r){ authMatrix.push({actor, action, method, path:p, status:r.status}); return r; }
function gate(role, s){ roleGate[role]={ consoleRaw:s.consoleErr.length, consoleGenuine:s.consoleErr.filter(classifyErr).length, consoleGenuineList:s.consoleErr.filter(classifyErr), http4xx:s.http4xx.slice(), failed:s.failed.length, negotiate:s.negot.slice(), websockets:s.wsCount }; }

// ============ 1) empIn — in-scope access + raise execution proposal ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('empIn'));
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2500);
  await snap(s.page,'fig-rc-001','emp داخل النطاق — النظرة العامّة لمساحة المشروع 360 (تقدّم 40%).');
  await clickTab(s.page,'تحديثات التنفيذ');
  await snap(s.page,'fig-rc-002','emp داخل النطاق — تبويب «تحديثات التنفيذ» (يستطيع رفع ادّعاء تنفيذ).');
  // raise two proposals via API with emp's own JWT
  const p1 = rec('empIn','رفع ادّعاء P1','POST',`/projects/${PROJ}/execution-proposals`, await apiCall(s.page,'POST',`/projects/${PROJ}/execution-proposals`,{deliverableId:DEL_A,proposedProgressPercent:60,proposedStatus:'InProgress',executionNote:'UAT-RC ادّعاء P1'}));
  const p2 = rec('empIn','رفع ادّعاء P2','POST',`/projects/${PROJ}/execution-proposals`, await apiCall(s.page,'POST',`/projects/${PROJ}/execution-proposals`,{deliverableId:DEL_A,proposedProgressPercent:70,proposedStatus:'InProgress',executionNote:'UAT-RC ادّعاء P2'}));
  const P1=(p1.data&&p1.data.id)||null, P2=(p2.data&&p2.data.id)||null;
  // negative: emp cannot review own proposal
  if(P1) rec('empIn','مراجعة (ممنوعة)','PATCH',`/projects/${PROJ}/execution-proposals/${P1}/review`, await apiCall(s.page,'PATCH',`/projects/${PROJ}/execution-proposals/${P1}/review`,{accept:true,reviewNote:'x'}));
  gate('empIn', s);
  await s.ctx.close();
  globalThis.__P1=P1; globalThis.__P2=P2;
}

// ============ 2) empOut — out of scope => 404 ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('empOut'));
  const ov = rec('empOut','قراءة المشروع (خارج النطاق)','GET',`/projects/${PROJ}/overview`, await apiCall(s.page,'GET',`/projects/${PROJ}/overview`));
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2800);
  await snap(s.page,'fig-rc-003','موظّف خارج النطاق — «المشروع غير موجود أو خارج نطاق صلاحيّتك» (404 حماية بالمورد).');
  gate('empOut', s);
  await s.ctx.close();
}

// ============ 3) TL — no structural buttons, reject P1, accept P2, propagation ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('tl'));
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2500);
  await snap(s.page,'fig-rc-004','قائد الفريق — صفحة المشروع بلا زرَّي «تعديل/أرشفة المشروع» (منع بنيويّ).');
  const tlEditVisible = await s.page.getByRole('button',{name:'تعديل المشروع'}).count();
  const tlArchiveVisible = await s.page.getByText('أرشفة المشروع',{exact:false}).count();
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2000);
  await clickTab(s.page,'تحديثات التنفيذ');
  await snap(s.page,'fig-rc-005','قائد الفريق — قائمة اقتراحات التنفيذ المعلّقة قبل الحسم.');
  const P1=globalThis.__P1, P2=globalThis.__P2;
  if(P1) rec('tl','رفض P1 بسبب','PATCH',`/projects/${PROJ}/execution-proposals/${P1}/review`, await apiCall(s.page,'PATCH',`/projects/${PROJ}/execution-proposals/${P1}/review`,{accept:false,reviewNote:'UAT-RC رفض موثّق — النسبة غير مدعومة بدليل.'}));
  await s.page.reload({waitUntil:'domcontentloaded'}); await s.page.waitForTimeout(1500);
  await clickTab(s.page,'تحديثات التنفيذ');
  await snap(s.page,'fig-rc-006','قائد الفريق — بعد الرفض: الاقتراح «مرفوض» بسبب معلن (لم تُطبَّق أيّ نسبة).');
  if(P2) rec('tl','قبول P2 وتطبيقه','PATCH',`/projects/${PROJ}/execution-proposals/${P2}/review`, await apiCall(s.page,'PATCH',`/projects/${PROJ}/execution-proposals/${P2}/review`,{accept:true,reviewNote:'UAT-RC قبول — دليل كافٍ.'}));
  await apiCall(s.page,'POST',`/projects/${PROJ}/health/recompute`);
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2500);
  await snap(s.page,'fig-rc-007','قائد الفريق — انتشار الأثر بعد القبول: إعادة احتساب تقدّم المشروع في النظرة العامّة.');
  await clickTab(s.page,'المؤشّرات والقراءات');
  await snap(s.page,'fig-rc-015','قائد الفريق — تبويب «المؤشّرات والقراءات» (KPI مع قراءة).');
  roleGate['tl']={...{consoleRaw:s.consoleErr.length,consoleGenuine:s.consoleErr.filter(classifyErr).length,consoleGenuineList:s.consoleErr.filter(classifyErr),http4xx:s.http4xx.slice(),failed:s.failed.length,negotiate:s.negot.slice(),websockets:s.wsCount}, tlEditVisible, tlArchiveVisible};
  await s.ctx.close();
}

// ============ 4) owner — in-scope operational ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('owner'));
  rec('owner','قراءة المشروع','GET',`/projects/${PROJ}/overview`, await apiCall(s.page,'GET',`/projects/${PROJ}/overview`));
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2500);
  await snap(s.page,'fig-rc-008','مالك المشروع — داخل النطاق مع أدوات التشغيل (النظرة العامّة).');
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2000);
  await snap(s.page,'fig-rc-016','مالك المشروع — صفحة المشروع البنيويّة (تيّارات العمل/المخرَجات).');
  gate('owner', s);
  await s.ctx.close();
}

// ============ 5) AM — in-scope + Client360 propagation ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('am'));
  rec('am','قراءة المشروع','GET',`/projects/${PROJ}/overview`, await apiCall(s.page,'GET',`/projects/${PROJ}/overview`));
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2500);
  await snap(s.page,'fig-rc-009','مدير الحساب — داخل النطاق (النظرة العامّة للمشروع).');
  await s.page.goto(ORIGIN+`/app/clients/${CLIENT}`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2800);
  await snap(s.page,'fig-rc-014','مدير الحساب — ملفّ العميل الشامل (Client 360) مع انتشار المشروع.');
  gate('am', s);
  await s.ctx.close();
}

// ============ 6) Manager — structural buttons present + governance ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('mgr'));
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2500);
  await snap(s.page,'fig-rc-010','الإدارة (Manager) — صفحة المشروع تُظهر أزرار «تعديل/أرشفة» البنيويّة (تباين مقابل قائد الفريق).');
  const mgrEditVisible = await s.page.getByRole('button',{name:'تعديل المشروع'}).count();
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2000);
  await clickTab(s.page,'القرارات والحوكمة');
  await snap(s.page,'fig-rc-011','الإدارة (Manager) — تبويب «القرارات والحوكمة».');
  roleGate['mgr']={...{consoleRaw:s.consoleErr.length,consoleGenuine:s.consoleErr.filter(classifyErr).length,consoleGenuineList:s.consoleErr.filter(classifyErr),http4xx:s.http4xx.slice(),failed:s.failed.length,negotiate:s.negot.slice(),websockets:s.wsCount}, mgrEditVisible};
  await s.ctx.close();
}

// ============ 7) GM — structural buttons + governance ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('gm'));
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2500);
  await snap(s.page,'fig-rc-012','الإدارة (GM) — صفحة المشروع تُظهر الأزرار البنيويّة.');
  const gmEditVisible = await s.page.getByRole('button',{name:'تعديل المشروع'}).count();
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(2000);
  await clickTab(s.page,'القرارات والحوكمة');
  await snap(s.page,'fig-rc-013','الإدارة (GM) — تبويب «القرارات والحوكمة».');
  roleGate['gm']={...{consoleRaw:s.consoleErr.length,consoleGenuine:s.consoleErr.filter(classifyErr).length,consoleGenuineList:s.consoleErr.filter(classifyErr),http4xx:s.http4xx.slice(),failed:s.failed.length,negotiate:s.negot.slice(),websockets:s.wsCount}, gmEditVisible};
  await s.ctx.close();
}

// ============ 8) CEO — read + SignalR settle (browser gate anchor) ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('ceo'));
  await s.page.goto(ORIGIN+`/app/projects/${PROJ}/360`,{waitUntil:'domcontentloaded'});
  await s.page.waitForTimeout(12000); // settle SignalR
  rec('ceo','قراءة المشروع','GET',`/projects/${PROJ}/overview`, await apiCall(s.page,'GET',`/projects/${PROJ}/overview`));
  gate('ceo', s);
  await s.ctx.close();
}

// ============ 9) viewer — negative (not a Project360 role) ============
{
  const s = await makeCtx();
  await login(s.page, EMAIL('viewer'));
  rec('viewer','قراءة المشروع (خارج الدور)','GET',`/projects/${PROJ}/overview`, await apiCall(s.page,'GET',`/projects/${PROJ}/overview`));
  gate('viewer', s);
  await s.ctx.close();
}

await browser.close();

const results = { origin: ORIGIN, project: PROJ, figures, roleGate, authMatrix };
fs.writeFileSync(path.join(OUT,'results-rc.json'), JSON.stringify(results,null,1));
console.log('FIGURES='+figures.length);
console.log('AUTH_MATRIX='+JSON.stringify(authMatrix));
console.log('ROLE_GATE='+JSON.stringify(roleGate));
