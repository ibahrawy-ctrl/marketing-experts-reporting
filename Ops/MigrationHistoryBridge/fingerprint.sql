-- بصمة مخطَّط حتميّة (Deterministic Schema Fingerprint) — قراءة فقط، بلا أيّ تعديل.
-- تغطّي: الجداول والأعمدة وأنواعها وقابليّة العدم والافتراضيّات، والقيود، والفهارس (بما فيها فلاتر الفهارس الجزئيّة).
-- تُستثنى __EFMigrationsHistory عمدًا لأنّها سجلّ تاريخيّ لا مخطَّط (وهي محلّ الاختلاف المقصود بين البيئات).
WITH cols AS (
  SELECT format('C|%s|%s|%s|%s|%s|%s',
                c.table_name, c.column_name, c.data_type,
                coalesce(c.character_maximum_length::text,''),
                c.is_nullable, coalesce(c.column_default,'')) AS line
  FROM information_schema.columns c
  JOIN information_schema.tables t
    ON t.table_schema = c.table_schema AND t.table_name = c.table_name
  WHERE c.table_schema = 'public'
    AND t.table_type = 'BASE TABLE'
    AND c.table_name <> '__EFMigrationsHistory'
),
cons AS (
  SELECT format('K|%s|%s|%s', rel.relname, con.conname, pg_get_constraintdef(con.oid)) AS line
  FROM pg_constraint con
  JOIN pg_class rel ON rel.oid = con.conrelid
  JOIN pg_namespace ns ON ns.oid = rel.relnamespace
  WHERE ns.nspname = 'public' AND rel.relname <> '__EFMigrationsHistory'
),
idx AS (
  SELECT format('I|%s|%s|%s', tablename, indexname, indexdef) AS line
  FROM pg_indexes
  WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
)
SELECT line FROM (
  SELECT line FROM cols UNION ALL SELECT line FROM cons UNION ALL SELECT line FROM idx
) all_lines
ORDER BY line;
