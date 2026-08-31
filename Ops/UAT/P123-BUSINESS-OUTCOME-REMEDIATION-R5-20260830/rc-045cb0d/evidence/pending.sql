START TRANSACTION;

CREATE INDEX ix_submission_field_values_value_json_gin ON submission_field_values USING gin ("ValueJson" jsonb_path_ops);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260826185232_AddSubmissionFieldValueJsonGinIndex', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE kpi_template_assignments ADD "EffectiveFrom" date;

ALTER TABLE kpi_template_assignments ADD "EffectiveTo" date;

ALTER TABLE "AspNetUsers" ADD "ExitDate" date;

ALTER TABLE "AspNetUsers" ADD "HireDate" date;

CREATE INDEX "IX_kpi_template_assignments_ScopeType_ScopeId_EffectiveFrom_Ef~" ON kpi_template_assignments ("ScopeType", "ScopeId", "EffectiveFrom", "EffectiveTo");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260829214324_R5_DecOneCadenceEffectivityAndEmploymentWindow', '8.0.11');

COMMIT;

