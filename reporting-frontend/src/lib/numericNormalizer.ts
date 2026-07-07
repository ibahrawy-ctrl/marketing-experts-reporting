// أداة مركزية موحّدة لتطبيع الأرقام (RC-3 Task 2B) — مصدر الحقيقة الوحيد في الواجهة (لا تكرار).
// تحوّل الخانات العربية-الهندية (١٢٣ U+0660–U+0669) والفارسية (۱۲۳ U+06F0–U+06F9) إلى اللاتينية (123)
// أثناء الكتابة وقبل الإرسال، بحيث لا تغادر أي خانة عربية الواجهة إطلاقًا.

// يطبّع الخانات فقط (يحوّل رموز الأرقام دون المساس بالحروف/العلامات) — آمن لأي نصّ حرّ.
export function normalizeDigits(input: string): string {
  let out = '';
  for (const ch of input) {
    const code = ch.codePointAt(0)!;
    if (code >= 0x0660 && code <= 0x0669) out += String.fromCharCode(0x30 + (code - 0x0660));
    else if (code >= 0x06f0 && code <= 0x06f9) out += String.fromCharCode(0x30 + (code - 0x06f0));
    else out += ch;
  }
  return out;
}

// تنقية صارمة لخلية عمود رقمي: تطبّع ثم تُبقي الخانات + فاصلة عشرية واحدة + إشارة سالب في المقدّمة.
// تُزيل الحروف (abc/أبجد) والرموز ومعاملات الحساب (- في غير المقدّمة، / + * % ()).
export function sanitizeNumericInput(input: string): string {
  const normalized = normalizeDigits(input);
  let out = '';
  let hasDot = false;
  for (let i = 0; i < normalized.length; i++) {
    const c = normalized[i];
    if (c >= '0' && c <= '9') out += c;
    else if (c === '.' && !hasDot) { out += '.'; hasDot = true; }
    else if (c === '-' && out.length === 0) out += '-';
  }
  return out;
}

// قائمة سماح للأعمدة الرقمية (allowlist) — مصدر الحقيقة لأسماء الأعمدة العددية في كل سكيمات القوالب.
// القرار التصميمي: الافتراض «نصّ» لا «رقم». السبب: أعمدة النصّ الحرّ (نثر عربي مثل «أهم ما تم»/«المشكلة»/
// «الاعتراض»/«الكلمة») كثيرة وغير متوقّعة، فقائمة منع لها لا تكتمل أبدًا وتُتلِف نصًّا مشروعًا صامتًا.
// أما الأعمدة الرقمية فمجموعة منتهية معرّفة من مقاييس القوالب. لذا نُنقّي رقميًّا فقط ما نعرف يقينًا أنه عددي؛
// وأيّ عمود غير معروف يبقى نصًّا (تطبيع خانات فقط) — والخادم يضمن التطبيع/التجميع في كل الأحوال (فشل آمن).
const NUMERIC_GRID_COLUMNS = new Set<string>([
  // B2B/B2C ومشتري الإعلانات (إنجليزي/مختلط)
  'ساعات العمل', 'Contacted', 'Meetings', 'Proposals', 'Negotiation', 'Won', 'Revenue',
  'New Leads', 'Scraped Leads', 'Valid Leads', 'Follow-ups', 'Sales', 'Lost',
  'Qualified', 'Old Leads Worked', 'Requalified',
  'Spend', 'Leads', 'CPL', 'Purchases', 'CPA', 'ROAS',
  'Position', 'Impressions', 'Clicks', 'CTR',
  // أعمدة عربية عددية من قوالب البذر (TemplateSeeder)
  'المخطط', 'المنفذ', 'المتأخر', 'نسبة الإنجاز', 'المبلغ', 'عدد أيام التأخير',
  'المستهدف', 'المحقق', 'الترتيب السابق', 'الترتيب الحالي', 'عدد المنشورات',
  'ليدز', 'مكالمات', 'تسجيلات', 'قيمة المبيعات', 'التكرار', 'عدد الكلمات',
  'القيمة المتوقعة', 'الاحتمالية', 'الإنفاق', 'النتائج', 'العائد المتوقع',
  'متابعات', 'نسبة التحويل',
]);

// هل العمود (بالاسم) عمود رقمي؟ (فقط ما هو في قائمة السماح؛ ما عداه نصّ — فشل آمن).
export function isNumericGridColumn(columnName: string | undefined): boolean {
  if (!columnName) return false;
  return NUMERIC_GRID_COLUMNS.has(columnName.trim());
}
