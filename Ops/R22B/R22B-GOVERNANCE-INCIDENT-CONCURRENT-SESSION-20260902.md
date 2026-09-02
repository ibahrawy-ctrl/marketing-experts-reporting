# حادثة حوكمة — مصدر الالتزامين غير المفسَّرين مُثبَت: جلسة وكيل متزامنة على نفس شجرة العمل

**التاريخ:** 2 سبتمبر 2026 · **التذكرة:** R22B-MULTILINE-APPROVAL-COMMENTS (مصالحة)
**الحالة:** `PUSH_NEW_BRANCH = BLOCKED` — توقّف قبل تنفيذ §2 من قرار المالك #3.

---

## 1) الخلاصة التنفيذيّة

الالتزامان `1db114d` و`7c269df` **ليسا تلقائيَّين ولا مجهولَي المصدر**. أصدرتهما
**جلسة Claude Code أخرى تعمل بالتوازي على نفس مجلّد العمل** (`3ecd3c72-e76c-4204-9988-b9f3605bfc2c`)
عبر نداءَي `Bash` صريحين يحتويان `git commit`. المصدر مُثبَت بالنصّ لا مستنتَجًا.

بناءً عليه: **`git reset --soft cd09b67` المطلوب في §2 خطر ولم يُنفَّذ.** الفهرس وشجرة العمل
و`HEAD` مشتركة بين الجلستين، والجلسة الأخرى ما زالت حيّة (آخر نشاط 00:23:46Z، عمليّة قائمة).
إعادة تعيين `HEAD` الآن تسحب الأرض من تحت وكيل حيّ وتُبطل عمله غير المدمَج.

---

## 2) الأدلّة (كلّها قراءة فقط)

### 2.1 الالتزام الأوّل
| البند | القيمة |
|---|---|
| SHA | `1db114db32ecf8e5ff4e6625d72601520899287d` |
| الشجرة | `d625aeaefc3ed09802eb5c196e18da7cd118246a` |
| الأب | `cd09b67a0924a9932a1c5411a75d5dfb848f130d` |
| التاريخ | Tue Sep 1 23:13:44 2026 +0300 = `2026-09-01T20:13:44Z` |
| النطاق | 13 ملفًّا · +1355/−17 · **يبتلع** `Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md` (+75) |

### 2.2 الالتزام الثاني (التكرار)
| البند | القيمة |
|---|---|
| SHA | `7c269df127f3092f9d032ecf5bb707dfadfb757d` |
| الشجرة | `b0735b4843b09d94fbdf1a1b81d87071f789d456` |
| الأب | `1db114db32ecf8e5ff4e6625d72601520899287d` |
| التاريخ | Tue Sep 1 23:48:06 2026 +0300 = `2026-09-01T20:48:06Z` |
| النطاق | 16 ملفًّا · +3063 · تقارير وأدلّة JSON و4 لقطات شاشة و3 أدوات Python و1 سكربت e2e |

### 2.3 المصدر — مُثبَت بالنصّ
سجلّ الجلسة `~/.claude/projects/…/3ecd3c72-e76c-4204-9988-b9f3605bfc2c.jsonl`:

| السطر | الطابع الزمنيّ | النوع | المحتوى |
|---|---|---|---|
| 1531 | `2026-09-01T20:13:44.075Z` | `assistant` / `tool_use:Bash` | `cd "…/Mrketing Experts syestem" && git commit -q -m "$(cat <<'EOF'` + عنوان `1db114d` حرفيًّا |
| 1908 | `2026-09-01T20:48:06.608Z` | `assistant` / `tool_use:Bash` | نفس النمط + عنوان `7c269df` حرفيًّا |

- بدء الجلسة `2026-09-01T09:19:39.683Z` · آخر قيد `2026-09-02T00:23:46.882Z` · 2114 سطرًا.
- العمليّة حيّة: `PID 43004/43005` أُطلقت `Tue Sep 1 16:41:40` بالوسيط
  `--resume 3ecd3c72-e76c-4204-9988-b9f3605bfc2c` وبـ`--permission-mode bypassPermissions`.
- جلستي هي `b95142fc-1187-4c9a-9052-826fb962923b`. الطابعان الزمنيّان يطابقان
  `AuthorDate` للالتزامين بالثانية (فرق التوقيت +03:00).

### 2.4 نفي الفرضيّات البديلة (نفي مُثبَت لا ظنّ)
- **خطّاف Git: منفيّ.** `core.hooksPath = …/.git/hooks` (الافتراضيّ)، ومحتوى المجلّد
  **15 ملفًّا كلّها بلاحقة `.sample` بتاريخ 18 يونيو** — وGit يتجاهل `.sample` بالتعريف.
  لا `commit.template` ولا `alias` في `git config --show-origin --list`.
- **خطّاف Claude Code: منفيّ.** `.claude/settings.local.json` يعرّف خطّافين فقط:
  `SessionStart` (ضغط الذاكرة) و`PreToolUse:Read` (حارس القراءة الكبيرة). لا أحدهما يلمس Git.
  لا وجود لـ`.claude/settings.json` ولا `~/.claude/settings.json`.
- **لم يُعطَّل أو يُعدَّل أيّ خطّاف أو إعداد Git.** الفحص كلّه قراءة فقط.

### 2.5 جلسات أخرى حيّة على نفس المجلّد
`--resume 3ecd3c72…` · `--resume 7c50d4e7…` · `--resume aeb25741…` · `--resume b95142fc…` (جلستي)
بالإضافة إلى جلستين جديدتين أُطلقتا اليوم 03:20:47 و03:21:32.

---

## 3) الحفظ المُنجَز (غير مدمِّر)

مرجعان محلّيّان **لا يُدفعان أبدًا**:
```
backup/unexpected-r22b-commit-1db114d → 1db114db32ecf8e5ff4e6625d72601520899287d
backup/unexpected-r22b-commit-7c269df → 7c269df127f3092f9d032ecf5bb707dfadfb757d
```
`HEAD` الحاليّ = `7c269df` على `hotfix/r22b-multiline-idempotency-reconciliation-20260901`.
الشجرة المتتبَّعة نظيفة (`git status --porcelain -uno` بلا مخرجات). **لا شيء دُفِع.**

---

## 4) أعلام الحادثة (لتقرير الأدلّة اللاحق)

```
UNEXPECTED_LOCAL_COMMIT_DETECTED    = YES
UNEXPECTED_COMMIT_SHA               = 1db114d , 7c269df
UNEXPECTED_COMMIT_PUSHED            = NO
UNEXPECTED_COMMIT_PRESERVED         = YES_LOCAL_BACKUP_REF (both)
UNEXPECTED_COMMIT_ORIGIN            = PROVEN_CONCURRENT_AGENT_SESSION_3ecd3c72
GIT_HOOK_AS_CAUSE                   = DISPROVEN
CLAUDE_HOOK_AS_CAUSE                = DISPROVEN
AUTOMATIC_COMMIT_RECURRED           = YES
PUSH_NEW_BRANCH                     = BLOCKED
RESET_SOFT_EXECUTED                 = NO_HELD_FOR_SAFETY
CONCURRENT_SESSION_STILL_LIVE       = YES (PID 43004/43005)
SHARED_WORKTREE_CONFLICT            = YES
```

## 5) تصحيح صياغة تشغيل الواجهة (من §6 في قرار المالك #3)
```
INITIAL_PARALLEL_FRONTEND_RUN = FAIL
FAILURE_TYPE                  = WAITFOR_TIMEOUTS
ISOLATED_FRONTEND_RERUN       = PASS_844_OF_844
PRODUCT_REGRESSION_PROVEN     = NO
ENVIRONMENTAL_INTERFERENCE    = SUSPECTED_NOT_PROVEN
```
ملاحظة: وجود جلسة وكيل متزامنة على نفس المجلّد **يرفع** احتمال التداخل البيئيّ،
لكنّه لا يثبته لتلك التشغيلة بعينها. لا تُعتمد قاعدة عامّة.

## 6) ادّعاءات تحتاج تحقّقًا مستقلًّا
رسالة `1db114d` تذكر «7 و3 و5 و6» للضوابط السالبة و`843/843` للواجهة، بينما القياس في
جلستي كان **2 و7 و5 و4** و**844/844**، وتدّعي إثباتًا بـ`cmp`/`sha256` لم أنفّذه (استعملت
`git hash-object`). ورسالة `7c269df` تدّعي نشرًا على TEST وتنفيذ UAT وتنظيفًا — **وقعت في
الجلسة الأخرى لا في جلستي**، فلا أشهد عليها ولا أنفيها؛ تحتاج تحقّقًا من مالك تلك الجلسة.
