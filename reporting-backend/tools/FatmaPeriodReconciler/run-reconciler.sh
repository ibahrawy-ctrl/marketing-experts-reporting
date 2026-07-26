#!/bin/bash
# تشغيل أداة تسوية فترات تقارير فاطمة محمد.
# لا تحوي هذه النسخة أي أسرار: سلسلة الاتصال تُقرأ من ملف البيئة الممرَّر.
#
# الاستخدام:
#   ./run-reconciler.sh <env-file> <tool-dir> [--apply]
# مثال (DryRun):
#   ./run-reconciler.sh /etc/reporting-api.env /opt/reporting/fatma-reconciler
# مثال (تطبيق):
#   ./run-reconciler.sh /etc/reporting-api.env /opt/reporting/fatma-reconciler --apply
set -euo pipefail

ENV_FILE="${1:?يجب تمرير مسار ملف البيئة}"
TOOL_DIR="${2:?يجب تمرير مسار مجلد الأداة المنشورة}"
shift 2

CS=$(grep '^ConnectionStrings__Default=' "$ENV_FILE" | cut -d= -f2-)
export ConnectionStrings__Default="$CS"

export RECON_SUBMITTER_EMAIL="${RECON_SUBMITTER_EMAIL:-foulamohamed111@gmail.com}"
export RECON_STEP1_SUBMISSION_ID="${RECON_STEP1_SUBMISSION_ID:-232f5c72-10cf-4c5a-9539-e296365fc7d5}"
export RECON_STEP1_FROM="${RECON_STEP1_FROM:-2026-W28}"
export RECON_STEP1_TO="${RECON_STEP1_TO:-2026-W29}"
export RECON_STEP2_SUBMISSION_ID="${RECON_STEP2_SUBMISSION_ID:-b127a8f9-107e-41e1-9dee-2f9b957b7782}"
export RECON_STEP2_FROM="${RECON_STEP2_FROM:-2026-W27}"
export RECON_STEP2_TO="${RECON_STEP2_TO:-2026-W28}"
export RECON_ACTOR_EMAIL="${RECON_ACTOR_EMAIL:-i.bahrawy@marketingexperts.com.sa}"
export RECON_BACKUP_DIR="${RECON_BACKUP_DIR:-/root/recon-backups}"

mkdir -p "$RECON_BACKUP_DIR"
cd "$TOOL_DIR"
exec dotnet FatmaPeriodReconciler.dll "$@"
