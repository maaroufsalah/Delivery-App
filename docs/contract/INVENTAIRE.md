# Inventaire du contrat API Speedy

Produit par `node scripts/gen-inventory.mjs <speedy>/apps/api/src` — ne pas éditer à la main sauf pour cocher.

Total : **506 endpoints** dans 47 contrôleurs. Préfixe global `/api`.

Légende garde : `public` = sans session ; sinon JWT obligatoire. Les permissions sont celles de `@RequirePermission` (toutes requises).


## Module 0 — Hors périmètre (console /platform, mono-société)

### `modules/platform/platform.controller.ts`

- [-] `POST /api/platform/trial-requests` — `requestTrial` (L83) · public · gardes: PublicRateLimitGuard · entrée: trialRequestSchema · sortie: `Promise<void>`
- [-] `GET /api/platform/tenants` — `list` (L94) · perm: platform.read · entrée: tenantListQuerySchema
- [-] `GET /api/platform/audit` — `audit` (L101) · perm: platform.read · entrée: tenantFiltersSchema
- [-] `GET /api/platform/tech-audit` — `techAudit` (L113) · perm: platform.read · entrée: techAuditQuerySchema
- [-] `GET /api/platform/tech-audit/entry/:id` — `techAuditEntry` (L124) · perm: platform.read · entrée: techAuditEntryQuerySchema
- [-] `GET /api/platform/errors` — `errors` (L135) · perm: platform.read · entrée: serverErrorsQuerySchema
- [-] `GET /api/platform/errors/occurrences` — `errorOccurrences` (L142) · perm: platform.read · entrée: serverErrorOccurrencesQuerySchema
- [-] `POST /api/platform/errors/test` — `errorTest` (L159) · perm: platform.write · sortie: `never`
- [-] `POST /api/platform/tenants` — `create` (L172) · perm: platform.write · entrée: createTenantSchema
- [-] `PATCH /api/platform/tenants/:id/status` — `setStatus` (L192) · perm: platform.write · entrée: tenantStatusSchema
- [-] `PATCH /api/platform/tenants/:id/trial` — `extendTrial` (L209) · perm: platform.write · entrée: extendTrialSchema
- [-] `POST /api/platform/tenants/:id/impersonate` — `impersonate` (L235) · perm: platform.impersonate

## Module 2 — Identité et accès

### `modules/auth/auth.controller.ts`

- [x] `POST /api/auth/login` — `login` (L102) · public · entrée: loginSchema
- [x] `POST /api/auth/forgot-password` — `forgotPassword` (L172) · public · gardes: PublicRateLimitGuard · entrée: forgotPasswordSchema
- [x] `POST /api/auth/impersonate` — `impersonate` (L197) · public · entrée: impersonationExchangeSchema
- [x] `POST /api/auth/refresh` — `refresh` (L218) · public · entrée: refreshSchema
- [x] `POST /api/auth/logout` — `logout` (L271) · public · entrée: refreshSchema · sortie: `Promise<void>`
- [x] `GET /api/auth/me` — `me` (L303)
- [x] `DELETE /api/auth/me` — `deleteMe` (L366)

### `modules/health/health.controller.ts`

- [x] `GET /api/health` — `check` (L36) · public · sortie: `Promise<HealthBody>`

### `modules/public/public.controller.ts`

- [x] `POST /api/public/register` — `register` (L123) · public · gardes: PublicRateLimitGuard · entrée: registrationSchema · sortie: `Promise<RegistrationResult>`

### `modules/roles/roles.controller.ts`

- [x] `GET /api/roles` — `matrix` (L18) · perm: accounts.read
- [x] `PUT /api/roles` — `update` (L26) · perm: accounts.write · entrée: roleMatrixSchema
- [x] `POST /api/roles/reload` — `reload` (L43) · perm: accounts.write

### `modules/roles/user-permissions.controller.ts`

- [x] `GET /api/users/:id/permissions` — `get` (L19) · perm: accounts.read
- [x] `PUT /api/users/:id/permissions` — `update` (L27) · perm: accounts.write · entrée: userPermissionsSchema
- [x] `DELETE /api/users/:id/permissions` — `reset` (L38) · perm: accounts.write

### `modules/users/users.controller.ts`

- [x] `GET /api/users` — `list` (L70) · perm: accounts.read · entrée: listQuerySchema · query: role, agencyId
- [x] `GET /api/users/export` — `export` (L89) · perm: accounts.read · entrée: listQuerySchema · query: role, agencyId · sortie: `Promise<StreamableFile>`
- [x] `POST /api/users/me/password` — `changePassword` (L118) · entrée: changePasswordSchema, updateOwnProfileSchema
- [x] `PATCH /api/users/me` — `updateOwnProfile` (L139) · entrée: updateOwnProfileSchema
- [x] `GET /api/users/team` — `team` (L150) · perm: accounts.read · entrée: listQuerySchema
- [x] `GET /api/users/:id` — `detail` (L157) · perm: accounts.read
- [x] `POST /api/users` — `create` (L171) · perm: accounts.write · entrée: createUserSchema
- [x] `PATCH /api/users/:id` — `update` (L178) · perm: accounts.write · entrée: updateUserSchema
- [x] `PATCH /api/users/:id/active` — `setActive` (L189) · perm: accounts.write · entrée: toggleActiveSchema
- [x] `POST /api/users/:id/reset-password` — `resetPassword` (L200) · perm: accounts.write
- [x] `POST /api/users/:id/set-password` — `setPassword` (L215) · perm: accounts.write · entrée: setUserPasswordSchema
- [x] `DELETE /api/users/:id` — `remove` (L226) · perm: accounts.write

## Module 3 — Société et référentiel

### `modules/agencies/agencies.controller.ts`

- [x] `GET /api/agencies` — `list` (L52) · perm: accounts.read · entrée: listQuerySchema
- [x] `GET /api/agencies/export` — `export` (L66) · perm: accounts.read · entrée: listQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/agencies/:id` — `detail` (L80) · perm: accounts.read
- [x] `POST /api/agencies` — `create` (L87) · perm: accounts.write · entrée: agencySchema
- [x] `PATCH /api/agencies/:id` — `update` (L94) · perm: accounts.write · entrée: agencySchema
- [x] `PATCH /api/agencies/:id/active` — `setActive` (L105) · perm: accounts.write · entrée: toggleActiveSchema
- [x] `DELETE /api/agencies/:id` — `remove` (L116) · perm: accounts.write

### `modules/cities/cities.controller.ts`

- [x] `GET /api/cities` — `list` (L61) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: zoneId
- [x] `GET /api/cities/export` — `export` (L79) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: zoneId · sortie: `Promise<StreamableFile>`
- [x] `GET /api/cities/:id` — `detail` (L94) · gardes: NotMerchantGuard · perm: directory.read
- [x] `POST /api/cities` — `create` (L101) · gardes: NotMerchantGuard · perm: directory.write · entrée: citySchema
- [x] `PATCH /api/cities/:id` — `update` (L108) · gardes: NotMerchantGuard · perm: directory.write · entrée: citySchema
- [x] `PATCH /api/cities/:id/active` — `setActive` (L119) · gardes: NotMerchantGuard · perm: directory.write · entrée: toggleActiveSchema
- [x] `DELETE /api/cities/:id` — `remove` (L130) · gardes: NotMerchantGuard · perm: directory.write

### `modules/delivery-types/delivery-types.controller.ts`

- [x] `GET /api/delivery-types` — `list` (L51) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema
- [x] `GET /api/delivery-types/export` — `export` (L65) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/delivery-types/:id` — `detail` (L81) · gardes: NotMerchantGuard · perm: directory.read
- [x] `POST /api/delivery-types` — `create` (L88) · gardes: NotMerchantGuard · perm: directory.write · entrée: deliveryTypeSchema
- [x] `PATCH /api/delivery-types/:id` — `update` (L98) · gardes: NotMerchantGuard · perm: directory.write · entrée: deliveryTypeSchema
- [x] `PATCH /api/delivery-types/:id/active` — `setActive` (L109) · gardes: NotMerchantGuard · perm: directory.write · entrée: toggleActiveSchema
- [x] `DELETE /api/delivery-types/:id` — `remove` (L120) · gardes: NotMerchantGuard · perm: directory.write

### `modules/directory/directory.controller.ts`

- [x] `GET /api/directory/cities` — `cities` (L17) · perm: directory.read · query: includeInactive
- [x] `GET /api/directory/zones` — `zones` (L24) · perm: directory.read
- [x] `GET /api/directory/districts` — `districts` (L32) · perm: directory.read · query: cityId
- [x] `GET /api/directory/clients` — `clients` (L39) · perm: directory.read
- [x] `GET /api/directory/couriers` — `couriers` (L46) · perm: directory.read
- [x] `GET /api/directory/agencies` — `agencies` (L53) · perm: directory.read
- [x] `GET /api/directory/pending-counts` — `pendingCounts` (L67) · perm: directory.read

### `modules/districts/districts.controller.ts`

- [x] `GET /api/districts` — `list` (L52) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: cityId
- [x] `GET /api/districts/export` — `export` (L70) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: cityId · sortie: `Promise<StreamableFile>`
- [x] `GET /api/districts/:id` — `detail` (L85) · gardes: NotMerchantGuard · perm: directory.read
- [x] `POST /api/districts` — `create` (L92) · gardes: NotMerchantGuard · perm: directory.write · entrée: districtSchema
- [x] `PATCH /api/districts/:id` — `update` (L99) · gardes: NotMerchantGuard · perm: directory.write · entrée: districtSchema
- [x] `PATCH /api/districts/:id/active` — `setActive` (L110) · gardes: NotMerchantGuard · perm: directory.write · entrée: toggleActiveSchema
- [x] `DELETE /api/districts/:id` — `remove` (L121) · gardes: NotMerchantGuard · perm: directory.write

### `modules/pricing/pricing-rules.controller.ts`

- [x] `GET /api/pricing-rules` — `list` (L46) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: originCityId, destinationCityId
- [x] `GET /api/pricing-rules/by-origin` — `byOrigin` (L65) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: originCityId, zoneId
- [x] `PUT /api/pricing-rules/by-origin/:originCityId` — `saveOrigin` (L79) · gardes: NotMerchantGuard · perm: directory.write · entrée: pricingOriginSaveSchema
- [x] `GET /api/pricing-rules/by-origin/:originCityId/export` — `exportOrigin` (L90) · gardes: NotMerchantGuard · perm: directory.read · sortie: `Promise<StreamableFile>`
- [x] `POST /api/pricing-rules/by-origin/:originCityId/import` — `importOrigin` (L106) · gardes: NotMerchantGuard · perm: directory.write · entrée: pricingImportSchema
- [x] `POST /api/pricing-rules` — `create` (L117) · gardes: NotMerchantGuard · perm: directory.write · entrée: pricingRuleSchema
- [x] `PATCH /api/pricing-rules/:id` — `update` (L124) · gardes: NotMerchantGuard · perm: directory.write · entrée: pricingRuleSchema
- [x] `DELETE /api/pricing-rules/:id` — `remove` (L135) · gardes: NotMerchantGuard · perm: directory.write

### `modules/public/public.controller.ts`

- [x] `GET /api/public/company` — `company` (L35) · public · gardes: PublicRateLimitGuard
- [x] `GET /api/public/logo/:fileName` — `logo` (L59) · public · gardes: PublicRateLimitGuard · sortie: `Promise<StreamableFile>`
- [x] `GET /api/public/rates` — `rates` (L71) · public · gardes: PublicRateLimitGuard
- [x] `GET /api/public/mobile-config` — `mobileConfig` (L90) · public · gardes: PublicRateLimitGuard · sortie: `MobileConfig`
- [x] `GET /api/public/cities` — `cities` (L103) · public · gardes: PublicRateLimitGuard · sortie: `Promise<PublicCity[]>`

### `modules/settings/settings.controller.ts`

- [x] `GET /api/settings/company` — `getCompany` (L32) · perm: settings.read
- [x] `GET /api/settings/business` — `getBusiness` (L43) · perm: settings.read
- [x] `PATCH /api/settings/business` — `updateBusiness` (L50) · perm: settings.write · entrée: businessSettingsSchema
- [x] `GET /api/settings/finance` — `getFinance` (L67) · perm: settings.read
- [x] `PATCH /api/settings/finance` — `updateFinance` (L74) · perm: settings.write · entrée: financeSettingsSchema
- [x] `POST /api/settings/logo` — `uploadLogo` (L95) · perm: settings.write · entrée: companyLogoSchema
- [x] `PATCH /api/settings/company` — `updateCompany` (L106) · perm: settings.write · entrée: companySettingsPatchSchema
- [x] `GET /api/settings/theme` — `getThemeWorkbench` (L131) · perm: settings.read
- [x] `POST /api/settings/theme/restore/:which` — `restoreTheme` (L144) · perm: settings.write
- [x] `PATCH /api/settings/theme` — `updateTheme` (L163) · perm: settings.write · entrée: themeOverridesSchema

### `modules/zones/zones.controller.ts`

- [x] `GET /api/zones` — `list` (L60) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema
- [x] `GET /api/zones/export` — `export` (L74) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/zones/rates` — `rates` (L92) · gardes: NotMerchantGuard · perm: directory.read
- [x] `POST /api/zones/rates` — `upsertRate` (L99) · gardes: NotMerchantGuard · perm: directory.write · entrée: zoneRateSchema
- [x] `DELETE /api/zones/rates/:id` — `removeRate` (L106) · gardes: NotMerchantGuard · perm: directory.write
- [x] `GET /api/zones/:id` — `detail` (L113) · gardes: NotMerchantGuard · perm: directory.read
- [x] `POST /api/zones` — `create` (L120) · gardes: NotMerchantGuard · perm: directory.write · entrée: zoneSchema
- [x] `PATCH /api/zones/:id` — `update` (L127) · gardes: NotMerchantGuard · perm: directory.write · entrée: zoneSchema
- [x] `PATCH /api/zones/:id/active` — `setActive` (L138) · gardes: NotMerchantGuard · perm: directory.write · entrée: toggleActiveSchema
- [x] `DELETE /api/zones/:id` — `remove` (L149) · gardes: NotMerchantGuard · perm: directory.write

## Module 4 — Clients et livreurs

### `modules/clients/clients.controller.ts`

- [x] `GET /api/clients` — `list` (L85) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: agencyId, validated
- [x] `GET /api/clients/export` — `export` (L104) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: agencyId, validated · sortie: `Promise<StreamableFile>`
- [x] `GET /api/clients/:id` — `detail` (L120) · gardes: NotMerchantGuard · perm: directory.read
- [x] `POST /api/clients` — `create` (L127) · gardes: NotMerchantGuard · perm: directory.write · entrée: clientSchema
- [x] `PATCH /api/clients/:id` — `update` (L142) · gardes: NotMerchantGuard · perm: directory.write · entrée: clientPatchSchema
- [x] `PATCH /api/clients/:id/vat-rate` — `setVatRate` (L154) · gardes: NotMerchantGuard · perm: finance.validate · entrée: clientVatRateSchema
- [x] `PATCH /api/clients/:id/pickup-terms` — `setPickupTerms` (L166) · gardes: NotMerchantGuard · perm: finance.validate · entrée: clientPickupTermsSchema
- [x] `PATCH /api/clients/:id/active` — `setActive` (L177) · gardes: NotMerchantGuard · perm: directory.write · entrée: toggleActiveSchema
- [x] `PATCH /api/clients/:id/validation` — `setValidated` (L189) · gardes: NotMerchantGuard · perm: directory.write · entrée: toggleActiveSchema
- [x] `POST /api/clients/:id/validate` — `validate` (L208) · gardes: NotMerchantGuard · perm: directory.write · entrée: validateAccountSchema
- [x] `POST /api/clients/:id/reject` — `reject` (L220) · gardes: NotMerchantGuard · perm: directory.write · entrée: rejectAccountSchema
- [x] `POST /api/clients/:id/account` — `createAccount` (L233) · gardes: NotMerchantGuard · perm: accounts.write · entrée: createUserSchema
- [x] `DELETE /api/clients/:id` — `remove` (L244) · gardes: NotMerchantGuard · perm: directory.write
- [x] `POST /api/clients/:id/discounts` — `addDiscount` (L253) · gardes: NotMerchantGuard · perm: directory.write · entrée: clientDiscountSchema
- [x] `PATCH /api/clients/:id/discounts/:discountId` — `updateDiscount` (L264) · gardes: NotMerchantGuard · perm: directory.write · entrée: clientDiscountSchema
- [x] `DELETE /api/clients/:id/discounts/:discountId` — `removeDiscount` (L276) · gardes: NotMerchantGuard · perm: directory.write
- [x] `POST /api/clients/:id/pickup-addresses` — `addPickupAddress` (L289) · gardes: NotMerchantGuard · perm: directory.write · entrée: clientPickupAddressSchema
- [x] `DELETE /api/clients/:id/pickup-addresses/:addressId` — `removePickupAddress` (L300) · gardes: NotMerchantGuard · perm: directory.write
- [x] `POST /api/clients/:id/documents` — `addDocument` (L313) · gardes: NotMerchantGuard · perm: directory.write · entrée: clientDocumentSchema
- [x] `GET /api/clients/:id/documents/:documentId` — `readDocument` (L325) · gardes: NotMerchantGuard · perm: directory.read · sortie: `Promise<StreamableFile>`
- [x] `DELETE /api/clients/:id/documents/:documentId` — `removeDocument` (L343) · gardes: NotMerchantGuard · perm: directory.write

### `modules/couriers/couriers.controller.ts`

- [x] `GET /api/couriers` — `list` (L74) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: agencyId, cityId, validated
- [x] `GET /api/couriers/export` — `export` (L94) · gardes: NotMerchantGuard · perm: directory.read · entrée: listQuerySchema · query: agencyId, cityId, validated · sortie: `Promise<StreamableFile>`
- [x] `GET /api/couriers/:id` — `detail` (L111) · gardes: NotMerchantGuard · perm: directory.read
- [x] `POST /api/couriers` — `create` (L118) · gardes: NotMerchantGuard · perm: directory.write · entrée: courierSchema
- [x] `PATCH /api/couriers/:id` — `update` (L125) · gardes: NotMerchantGuard · perm: directory.write · entrée: courierSchema
- [x] `PATCH /api/couriers/:id/active` — `setActive` (L136) · gardes: NotMerchantGuard · perm: directory.write · entrée: toggleActiveSchema
- [x] `PATCH /api/couriers/:id/validation` — `setValidated` (L147) · gardes: NotMerchantGuard · perm: directory.write · entrée: toggleActiveSchema
- [x] `PUT /api/couriers/:id/cities` — `setCities` (L159) · gardes: NotMerchantGuard · perm: directory.write · entrée: courierCitiesSchema
- [x] `POST /api/couriers/:id/validate` — `validate` (L178) · gardes: NotMerchantGuard · perm: directory.write · entrée: validateAccountSchema
- [x] `POST /api/couriers/:id/reject` — `reject` (L190) · gardes: NotMerchantGuard · perm: directory.write · entrée: rejectAccountSchema
- [x] `POST /api/couriers/:id/account` — `createAccount` (L201) · gardes: NotMerchantGuard · perm: accounts.write · entrée: createUserSchema
- [x] `DELETE /api/couriers/:id` — `remove` (L212) · gardes: NotMerchantGuard · perm: directory.write

### `modules/merchant/merchant.controller.ts`

- [x] `GET /api/merchant/me/company` — `company` (L77) · gardes: MerchantGuard · perm: directory.read
- [x] `PATCH /api/merchant/me/company` — `updateCompany` (L83) · gardes: MerchantGuard · entrée: merchantCompanySchema

## Module 5 — Colis

### `modules/audit/audit.controller.ts`

- [x] `GET /api/audit` — `list` (L86) · gardes: TechnicalViewGuard · perm: audit.read · entrée: auditListQuerySchema
- [x] `GET /api/audit/export` — `export` (L98) · gardes: TechnicalViewGuard · perm: audit.read · entrée: auditListQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/audit/journal` — `journal` (L122) · gardes: NotMerchantGuard · perm: audit.read · entrée: journalQuerySchema
- [x] `GET /api/audit/journal/export` — `journalExport` (L134) · gardes: NotMerchantGuard · perm: audit.read · entrée: journalQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/audit/journal/entry/:id` — `journalEntry` (L159) · gardes: NotMerchantGuard · perm: audit.read
- [x] `GET /api/audit/journal/entity/:entity/:entityId` — `journalForEntity` (L170) · gardes: NotMerchantGuard · entrée: journalQuerySchema
- [x] `GET /api/audit/entry/:id` — `detail` (L184) · gardes: TechnicalViewGuard · perm: audit.read
- [x] `GET /api/audit/:entity/:entityId` — `forEntity` (L199) · gardes: NotMerchantGuard, TechnicalViewGuard · entrée: auditListQuerySchema

### `modules/courier/courier.controller.ts`

- [x] `GET /api/courier/me` — `me` (L196) · gardes: CourierGuard · perm: parcels.read

### `modules/merchant/merchant.controller.ts`

- [x] `GET /api/merchant/me/summary` — `summary` (L50) · gardes: MerchantGuard · perm: parcels.read

### `modules/parcels/exchanges.controller.ts`

- [x] `GET /api/exchanges` — `list` (L37) · perm: parcels.read · entrée: exchangeListQuerySchema
- [x] `GET /api/exchanges/:code` — `detail` (L47) · perm: parcels.read
- [x] `POST /api/exchanges` — `create` (L60) · gardes: NotMerchantGuard · perm: parcels.write · entrée: exchangeCreateSchema
- [x] `POST /api/exchanges/:code/pickup` — `pickup` (L71) · gardes: NotMerchantGuard · perm: parcels.write · entrée: exchangeAdvanceSchema
- [x] `POST /api/exchanges/:code/deliver` — `deliver` (L83) · gardes: NotMerchantGuard · perm: parcels.write · entrée: exchangeAdvanceSchema
- [x] `POST /api/exchanges/:code/close` — `close` (L95) · gardes: NotMerchantGuard · perm: parcels.write · entrée: exchangeAdvanceSchema
- [x] `POST /api/exchanges/:code/cancel` — `cancel` (L107) · gardes: NotMerchantGuard · perm: parcels.write · entrée: exchangeAdvanceSchema

### `modules/parcels/parcels.controller.ts`

- [x] `GET /api/parcels` — `list` (L175) · perm: parcels.read · entrée: parcelListQuerySchema
- [x] `GET /api/parcels/export` — `export` (L194) · perm: parcels.read · entrée: parcelListQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/parcels/stats` — `stats` (L217) · perm: parcels.read · entrée: parcelListQuerySchema
- [x] `GET /api/parcels/next-code` — `nextCode` (L227) · perm: parcels.write · entrée: nextCodeQuerySchema
- [x] `POST /api/parcels/quote` — `quote` (L239) · perm: parcels.write · entrée: quoteSchema
- [x] `GET /api/parcels/import/template.xlsx` — `importTemplate` (L256) · perm: parcels.write · sortie: `Promise<StreamableFile>`
- [x] `POST /api/parcels/import/preview` — `previewImport` (L267) · perm: parcels.write · entrée: parcelImportSourceSchema
- [x] `POST /api/parcels/import/anomalies.xlsx` — `importAnomalies` (L284) · perm: parcels.write · entrée: parcelImportSourceSchema · sortie: `Promise<StreamableFile>`
- [x] `POST /api/parcels/import` — `runImport` (L296) · perm: parcels.write · entrée: parcelImportApplySchema
- [x] `GET /api/parcels/imports` — `importHistory` (L306) · perm: parcels.read · entrée: parcelImportHistorySchema
- [x] `GET /api/parcels/imports/:id/report.xlsx` — `importReport` (L317) · perm: parcels.read · sortie: `Promise<StreamableFile>`
- [x] `GET /api/parcels/labels` — `labelBatch` (L344) · perm: parcels.read · entrée: labelBatchQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `POST /api/parcels/labels` — `labelSelection` (L367) · perm: parcels.read · entrée: labelSelectionSchema · sortie: `Promise<StreamableFile>`
- [x] `POST /api/parcels/label-jobs` — `createLabelJob` (L403) · perm: parcels.read · entrée: labelSelectionSchema
- [x] `GET /api/parcels/label-jobs` — `labelJobList` (L414) · perm: parcels.read
- [x] `GET /api/parcels/label-jobs/:id` — `labelJob` (L422) · perm: parcels.read
- [x] `GET /api/parcels/label-jobs/:id/file` — `labelJobFile` (L430) · perm: parcels.read · sortie: `Promise<StreamableFile>`
- [x] `GET /api/parcels/:code` — `detail` (L446) · perm: parcels.read · query: deleted
- [x] `GET /api/parcels/:code/label` — `label` (L463) · perm: parcels.read · entrée: labelQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `POST /api/parcels` — `create` (L479) · perm: parcels.write · entrée: createParcelSchema · sortie: `l'appli envoie la * même clé au premier envoi et à chaque rejeu de sa file hors-ligne. * Bornée à 80 caractères imprimables ; autre chose est ignoré. */ @Headers('idempotency-key') idempotencyKey?: string, )`
- [x] `POST /api/parcels/:code/events` — `changeStatus` (L510) · gardes: NotMerchantGuard · perm: parcels.status · entrée: changeStatusBodySchema
- [x] `PATCH /api/parcels/:code/fees` — `editFees` (L534) · perm: parcels.editFees · entrée: editParcelFeesSchema
- [x] `PATCH /api/parcels/:code/destination` — `changeDestination` (L563) · gardes: NotMerchantGuard · perm: parcels.write · entrée: changeDestinationSchema
- [x] `PATCH /api/parcels/:code` — `updateBySeller` (L595) · gardes: MerchantGuard · perm: parcels.write · entrée: sellerParcelUpdateSchema
- [x] `DELETE /api/parcels/:code` — `removeBySeller` (L607) · gardes: MerchantGuard · perm: parcels.write
- [x] `POST /api/parcels/:code/admin-delete` — `removeByAdmin` (L621) · gardes: NotMerchantGuard · perm: parcels.write · entrée: parcelAdminDeleteSchema, parcelClientChangePreviewSchema · sortie: `Promise<ParcelClientChangePreview>`
- [x] `POST /api/parcels/:code/client/preview` — `previewClientChange` (L635) · gardes: NotMerchantGuard · perm: parcels.write · entrée: parcelClientChangePreviewSchema · sortie: `Promise<ParcelClientChangePreview>`
- [x] `POST /api/parcels/:code/client` — `changeClient` (L648) · gardes: NotMerchantGuard · perm: parcels.write · entrée: parcelClientChangeSchema · sortie: `Promise<ParcelClientChangeResult>`
- [x] `PATCH /api/parcels/:code/expected-payment` — `changeExpectedPayment` (L664) · gardes: NotMerchantGuard · perm: parcels.write · entrée: expectedPaymentChangeSchema
- [x] `PATCH /api/parcels/:code/bl` — `setBlRequested` (L675) · perm: parcels.write · entrée: parcelBlRequestSchema
- [x] `POST /api/parcels/:code/exchange` — `exchange` (L702) · perm: parcels.write · entrée: parcelExchangeSchema
- [x] `POST /api/parcels/:code/notes` — `addNote` (L715) · gardes: NotMerchantGuard · perm: parcels.write · entrée: addParcelNoteSchema

### `modules/provider/provider-tokens.controller.ts`

- [x] `GET /api/settings/providers` — `list` (L26) · perm: settings.read
- [x] `POST /api/settings/providers` — `create` (L33) · perm: settings.write · entrée: providerTokenCreateSchema
- [x] `POST /api/settings/providers/:id/rotate` — `rotate` (L43) · perm: settings.write
- [x] `POST /api/settings/providers/:id/revoke` — `revoke` (L50) · perm: settings.write
- [x] `PATCH /api/settings/providers/:id/mode` — `setMode` (L57) · perm: settings.write · entrée: providerTokenModeSchema
- [x] `PUT /api/settings/providers/:id/status-map` — `setStatusMap` (L68) · perm: settings.write · entrée: providerStatusMapUpdateSchema
- [x] `POST /api/settings/providers/:id/calls/:callId/resolve` — `resolveAlert` (L79) · perm: settings.write
- [x] `GET /api/settings/providers/:id/calls` — `calls` (L90) · perm: settings.read

### `modules/public/public.controller.ts`

- [x] `GET /api/public/track/:code` — `track` (L132) · public · gardes: PublicRateLimitGuard

### `modules/warehouse/orders.controller.ts`

- [x] `GET /api/orders` — `list` (L71) · perm: warehouse.read · entrée: orderListQuerySchema
- [x] `GET /api/orders/export` — `export` (L88) · perm: warehouse.read · entrée: orderListQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `POST /api/orders/import` — `import` (L104) · perm: warehouse.write · entrée: warehouseImportSchema
- [x] `GET /api/orders/:code` — `detail` (L118) · perm: warehouse.read
- [x] `POST /api/orders` — `create` (L125) · perm: warehouse.write · entrée: orderSchema
- [x] `POST /api/orders/:code/confirm` — `confirm` (L132) · perm: warehouse.write
- [x] `POST /api/orders/:code/parcel` — `createParcel` (L141) · perm: warehouse.write · entrée: orderToParcelSchema
- [x] `POST /api/orders/:code/cancel` — `cancel` (L152) · perm: warehouse.write
- [x] `DELETE /api/orders/:code` — `remove` (L159) · perm: warehouse.write

### `modules/warehouse/products.controller.ts`

- [x] `GET /api/products` — `list` (L75) · perm: warehouse.read · entrée: productListQuerySchema
- [x] `GET /api/products/export` — `export` (L92) · perm: warehouse.read · entrée: productListQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/products/attributes` — `attributes` (L108) · perm: warehouse.read
- [x] `POST /api/products/attributes` — `createAttribute` (L115) · perm: warehouse.write · entrée: productAttributeSchema
- [x] `POST /api/products/attributes/values` — `createAttributeValue` (L125) · perm: warehouse.write · entrée: productAttributeValueSchema
- [x] `POST /api/products/import` — `import` (L137) · perm: warehouse.write · entrée: warehouseImportSchema
- [x] `GET /api/products/:id` — `detail` (L151) · perm: warehouse.read
- [x] `GET /api/products/:id/image` — `image` (L159) · perm: warehouse.read · sortie: `Promise<StreamableFile>`
- [x] `POST /api/products` — `create` (L167) · perm: warehouse.write · entrée: productSchema
- [x] `PATCH /api/products/:id` — `update` (L174) · perm: warehouse.write · entrée: productSchema
- [x] `POST /api/products/:id/image` — `setImage` (L185) · perm: warehouse.write · entrée: productImageSchema
- [x] `DELETE /api/products/:id` — `remove` (L196) · perm: warehouse.write

### `modules/warehouse/stock.controller.ts`

- [x] `GET /api/stock` — `list` (L45) · perm: warehouse.read · entrée: stockListQuerySchema
- [x] `GET /api/stock/export` — `export` (L62) · perm: warehouse.read · entrée: stockListQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/stock/:id` — `detail` (L76) · perm: warehouse.read
- [x] `POST /api/stock/adjust` — `adjust` (L83) · perm: warehouse.write · entrée: stockAdjustSchema

### `modules/warehouse/storage-requests.controller.ts`

- [x] `GET /api/storage-requests` — `list` (L60) · perm: warehouse.read · entrée: storageListQuerySchema
- [x] `GET /api/storage-requests/export` — `export` (L77) · perm: warehouse.read · entrée: storageListQuerySchema · sortie: `Promise<StreamableFile>`
- [x] `GET /api/storage-requests/:code` — `detail` (L91) · perm: warehouse.read
- [x] `GET /api/storage-requests/:code/receipt` — `receipt` (L99) · perm: warehouse.read · sortie: `Promise<StreamableFile>`
- [x] `POST /api/storage-requests` — `create` (L114) · perm: warehouse.write · entrée: storageRequestSchema
- [x] `POST /api/storage-requests/:code/decision` — `decide` (L126) · perm: warehouse.validate · entrée: storageDecisionSchema
- [x] `DELETE /api/storage-requests/:code` — `remove` (L137) · perm: warehouse.write

## Module 6 — Bons et circuits

### `modules/bulk-jobs/bulk-jobs.controller.ts`

- [ ] `GET /api/bulk-jobs` — `latest` (L25) · gardes: NotMerchantGuard · perm: parcels.read · query: target · sortie: `Promise<BulkJob | null>`
- [ ] `GET /api/bulk-jobs/:id` — `one` (L34) · gardes: NotMerchantGuard · perm: parcels.read · sortie: `Promise<BulkJob>`
- [ ] `POST /api/bulk-jobs/:id/retry` — `retry` (L43) · gardes: NotMerchantGuard · perm: parcels.status · sortie: `Promise<BulkJob>`

### `modules/courier/courier-parcels.controller.ts`

- [ ] `GET /api/courier/parcels/:code/outcomes` — `outcomes` (L44) · gardes: CourierGuard · perm: parcels.read
- [ ] `POST /api/courier/parcels/:code/status` — `report` (L54) · gardes: CourierGuard · perm: parcels.status · entrée: routeConfirmSchema
- [ ] `POST /api/courier/parcels/:code/proof` — `proof` (L80) · gardes: CourierGuard · perm: parcels.status · entrée: proofPhotoSchema

### `modules/courier/courier.controller.ts`

- [ ] `GET /api/courier/me/performance` — `performance` (L96) · gardes: CourierGuard · perm: parcels.read · entrée: courierPerformanceQuerySchema
- [ ] `GET /api/courier/me/notes` — `notes` (L114) · gardes: CourierGuard · perm: routes.read · entrée: courierNotesQuerySchema
- [ ] `GET /api/courier/me/routes` — `routes` (L205) · gardes: CourierGuard · perm: routes.read · entrée: courierRoutesQuerySchema
- [ ] `GET /api/courier/me/deliveries` — `deliveries` (L218) · gardes: CourierGuard · perm: routes.read
- [ ] `GET /api/courier/me/routes/:code` — `route` (L225) · gardes: CourierGuard · perm: routes.read
- [ ] `POST /api/courier/me/scan` — `scan` (L242) · gardes: CourierGuard · perm: routes.scan · entrée: scanSchema
- [ ] `DELETE /api/courier/me/routes/:code/parcels/:parcelId` — `removeParcel` (L264) · gardes: CourierGuard · perm: routes.scan
- [ ] `POST /api/courier/me/routes/:code/dispatch` — `dispatch` (L305) · gardes: CourierGuard · perm: routes.confirm
- [ ] `POST /api/courier/me/routes/:code/notes/:noteId/pickup` — `pickupNote` (L329) · gardes: CourierGuard · perm: routes.confirm · entrée: routeNotePickupSchema

### `modules/courier-stock/courier-stock.controller.ts`

- [ ] `GET /api/courier-stock` — `list` (L100) · perm: routes.stock · entrée: courierStockListQuerySchema
- [ ] `GET /api/courier-stock/export` — `export` (L115) · perm: routes.stock · entrée: courierStockListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/courier-stock/pdf` — `globalPdf` (L127) · perm: routes.stock · entrée: courierStockListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/courier-stock/pdf-jobs` — `createPdfJob` (L142) · perm: routes.stock · entrée: courierStockPdfJobSchema
- [ ] `GET /api/courier-stock/pdf-jobs/:id` — `pdfJob` (L153) · perm: routes.stock
- [ ] `GET /api/courier-stock/pdf-jobs/:id/file` — `pdfJobFile` (L161) · perm: routes.stock · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/courier-stock/parcels/export` — `exportParcels` (L170) · perm: routes.stock · entrée: courierStockParcelExportQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/courier-stock/:courierId` — `detail` (L183) · perm: routes.stock · entrée: courierStockParcelQuerySchema
- [ ] `GET /api/courier-stock/:courierId/pdf` — `courierPdf` (L195) · perm: routes.stock · entrée: courierStockParcelQuerySchema · sortie: `Promise<StreamableFile>`

### `modules/merchant/merchant.controller.ts`

- [ ] `POST /api/merchant/me/pickups` — `requestPickup` (L94) · gardes: MerchantGuard · perm: notes.request · entrée: pickupRequestSchema
- [ ] `POST /api/merchant/me/dropoffs` — `requestDropoff` (L112) · gardes: MerchantGuard · perm: notes.request · entrée: pickupRequestSchema
- [ ] `DELETE /api/merchant/me/notes/:code` — `cancelNote` (L129) · gardes: MerchantGuard · perm: notes.request

### `modules/provider/provider.controller.ts`

- [ ] `POST /api/provider/parcels/:code/status` — `report` (L52) · public · gardes: ProviderTokenGuard, NotMerchantGuard · perm: parcels.status · entrée: providerStatusSchema

### `modules/routes/parcel-moves.controller.ts`

- [ ] `POST /api/parcels/field-status` — `changeFieldStatus` (L75) · gardes: NotMerchantGuard · perm: parcels.status · entrée: adminFieldStatusSchema · sortie: `Promise<AdminFieldStatusResult>`
- [ ] `POST /api/parcels/:code/correction/preview` — `previewCorrection` (L92) · gardes: NotMerchantGuard · perm: parcels.correctStatus · entrée: statusCorrectionSchema · sortie: `Promise<StatusCorrectionPreview>`
- [ ] `POST /api/parcels/:code/correction` — `applyCorrection` (L105) · gardes: NotMerchantGuard · perm: parcels.correctStatus · entrée: statusCorrectionSchema · sortie: `Promise<StatusCorrectionResult>`
- [ ] `POST /api/parcels/:code/cancel-delivery` — `cancelDelivery` (L118) · gardes: NotMerchantGuard · perm: parcels.status · entrée: cancelDeliverySchema
- [ ] `POST /api/routes/transfer` — `transfer` (L142) · gardes: NotMerchantGuard · perm: routes.write · entrée: parcelTransferSchema
- [ ] `POST /api/routes/transfer/options` — `transferOptions` (L162) · gardes: NotMerchantGuard · perm: routes.write · entrée: parcelTransferOptionsSchema

### `modules/routes/routes.controller.ts`

- [ ] `GET /api/routes` — `list` (L129) · perm: routes.read · entrée: routeListQuerySchema
- [ ] `GET /api/routes/stats` — `stats` (L143) · perm: routes.read · entrée: routeListQuerySchema
- [ ] `GET /api/routes/export` — `export` (L160) · perm: routes.read · entrée: routeListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/routes` — `create` (L175) · perm: routes.write · entrée: routeSchema
- [ ] `POST /api/routes/auto-close-sweep` — `sweepAutoClose` (L189) · perm: routes.close
- [ ] `GET /api/routes/:code` — `detail` (L196) · perm: routes.read
- [ ] `PATCH /api/routes/:code` — `update` (L207) · perm: routes.write · entrée: routeUpdateSchema
- [ ] `DELETE /api/routes/:code` — `remove` (L219) · perm: routes.write
- [ ] `GET /api/routes/:code/candidates` — `candidates` (L228) · perm: routes.read · entrée: routeCandidatesQuerySchema
- [ ] `GET /api/routes/:code/note-candidates` — `noteCandidates` (L256) · perm: routes.read · entrée: routeCandidatesQuerySchema
- [ ] `POST /api/routes/:code/notes` — `addNotes` (L272) · perm: routes.write · entrée: routeNotesSchema
- [ ] `DELETE /api/routes/:code/notes/:noteId` — `removeNote` (L283) · perm: routes.write
- [ ] `POST /api/routes/:code/notes/:noteId/pickup` — `pickupNote` (L303) · perm: routes.confirm · entrée: routeNotePickupSchema
- [ ] `POST /api/routes/:code/parcels` — `addParcels` (L315) · perm: routes.write · entrée: routeParcelsSchema
- [ ] `DELETE /api/routes/:code/parcels/:parcelId` — `removeParcel` (L326) · perm: routes.write
- [ ] `GET /api/routes/:code/export` — `exportParcels` (L338) · perm: routes.read · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/routes/:code/sheet` — `sheet` (L357) · perm: routes.read · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/routes/:code/labels` — `labels` (L370) · perm: routes.read · entrée: labelQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/routes/:code/dispatch` — `dispatch` (L389) · perm: routes.write
- [ ] `POST /api/routes/:code/parcels/:parcelId/confirm` — `confirm` (L407) · perm: routes.confirm · entrée: routeConfirmSchema
- [ ] `POST /api/routes/:code/outcomes` — `confirmMany` (L422) · perm: routes.confirm · entrée: routeBulkConfirmSchema
- [ ] `POST /api/routes/:code/close` — `close` (L442) · perm: routes.close · entrée: routeCloseSchema
- [ ] `GET /api/delivery-notes` — `list` (L503) · perm: notes.read · entrée: noteListQuerySchema
- [ ] `GET /api/delivery-notes/stats` — `stats` (L513) · perm: notes.read · entrée: noteListQuerySchema
- [ ] `GET /api/delivery-notes/export` — `export` (L530) · perm: notes.read · entrée: noteListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/delivery-notes` — `create` (L544) · perm: notes.write · entrée: deliveryNoteSchema
- [ ] `GET /api/delivery-notes/:code` — `detail` (L554) · perm: notes.read
- [ ] `GET /api/delivery-notes/:code/export` — `exportParcels` (L566) · perm: notes.read · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/delivery-notes/:code/pdf` — `pdf` (L585) · perm: notes.read · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/delivery-notes/:code/parcels` — `addParcels` (L597) · perm: notes.write · entrée: deliveryNoteParcelsSchema
- [ ] `DELETE /api/delivery-notes/:code/parcels/:parcelId` — `removeParcel` (L608) · perm: notes.write
- [ ] `POST /api/delivery-notes/:code/validate` — `validate` (L622) · perm: notes.write
- [ ] `POST /api/delivery-notes/:code/removed/:parcelId/restore` — `restoreRemoved` (L643) · perm: notes.write
- [ ] `DELETE /api/delivery-notes/:code` — `remove` (L662) · perm: notes.write

### `modules/staff/staff.controller.ts`

- [ ] `GET /api/staff/me/alerts` — `list` (L32) · gardes: StaffGuard · perm: parcels.read · query: agencyId

## Module 7 — Retours

### `modules/bl/bl.controller.ts`

- [ ] `POST /api/bl/parcels/:code/collect` — `collect` (L76) · perm: returns.write · entrée: blCollectSchema
- [ ] `GET /api/bl/parcels/:code` — `tracking` (L93) · perm: returns.read
- [ ] `GET /api/bl/delivery-slips` — `listDeliverySlips` (L105) · perm: returns.read · entrée: deliverySlipListQuerySchema
- [ ] `GET /api/bl/delivery-slips/export` — `exportDeliverySlips` (L117) · perm: returns.read · entrée: deliverySlipListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/bl/delivery-slips/advance` — `advanceDeliverySlips` (L138) · perm: returns.confirm · entrée: deliverySlipAdvanceSchema
- [ ] `POST /api/bl/delivery-slips/:id/revert` — `revertDeliverySlip` (L149) · perm: returns.confirm · entrée: deliverySlipRevertSchema
- [ ] `GET /api/bl/slips` — `slips` (L163) · gardes: NotMerchantGuard · perm: returns.read · entrée: blSlipListQuerySchema
- [ ] `GET /api/bl/notes` — `listNotes` (L173) · perm: returns.read · entrée: blNoteListQuerySchema
- [ ] `GET /api/bl/notes/export` — `exportNotes` (L190) · perm: returns.read · entrée: blNoteListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/bl/notes/:code` — `noteDetail` (L204) · perm: returns.read
- [ ] `POST /api/bl/notes` — `createNote` (L211) · perm: returns.write · entrée: blNoteSchema
- [ ] `POST /api/bl/notes/:code/slips` — `addSlips` (L218) · perm: returns.write · entrée: blNoteItemsSchema
- [ ] `DELETE /api/bl/notes/:code/slips/:slipId` — `removeSlip` (L229) · perm: returns.write
- [ ] `POST /api/bl/notes/:code/handover` — `handOver` (L240) · perm: returns.write
- [ ] `POST /api/bl/notes/:code/receive` — `receive` (L258) · perm: returns.confirm
- [ ] `POST /api/bl/notes/:code/return` — `returnToClient` (L265) · perm: returns.confirm · entrée: blNoteReturnSchema
- [ ] `POST /api/bl/notes/:code/close` — `close` (L276) · perm: returns.confirm
- [ ] `GET /api/bl/notes/:code/pdf` — `pdf` (L284) · perm: returns.read · sortie: `Promise<StreamableFile>`
- [ ] `DELETE /api/bl/notes/:code` — `removeNote` (L296) · perm: returns.write

### `modules/returns/returns.controller.ts`

- [ ] `GET /api/return-handovers` — `list` (L125) · gardes: NotMerchantGuard · perm: returns.read · entrée: handoverListQuerySchema
- [ ] `GET /api/return-handovers/export` — `export` (L142) · gardes: NotMerchantGuard · perm: returns.read · entrée: handoverListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/return-handovers/candidates` — `candidates` (L168) · gardes: NotMerchantGuard, NotMerchantGuard · perm: returns.read · entrée: handoverCandidatesQuerySchema
- [ ] `GET /api/return-handovers/bulk` — `bulkPreview` (L185) · gardes: NotMerchantGuard · perm: returns.read
- [ ] `POST /api/return-handovers/bulk` — `createBulk` (L194) · gardes: NotMerchantGuard · perm: returns.write
- [ ] `GET /api/return-handovers/:code` — `detail` (L201) · gardes: NotMerchantGuard · perm: returns.read
- [ ] `POST /api/return-handovers` — `create` (L208) · gardes: NotMerchantGuard · perm: returns.write · entrée: returnHandoverSchema
- [ ] `POST /api/return-handovers/:code/parcels` — `addParcel` (L220) · gardes: NotMerchantGuard · perm: returns.write · entrée: returnScanSchema
- [ ] `POST /api/return-handovers/:code/parcels/batch` — `addParcels` (L239) · gardes: NotMerchantGuard · perm: returns.write · entrée: returnHandoverParcelsSchema
- [ ] `DELETE /api/return-handovers/:code/parcels/:parcelId` — `removeParcel` (L250) · gardes: NotMerchantGuard · perm: returns.write
- [ ] `POST /api/return-handovers/:code/request` — `request` (L268) · gardes: NotMerchantGuard · perm: returns.write
- [ ] `POST /api/return-handovers/:code/pickup` — `pickup` (L283) · gardes: NotMerchantGuard · perm: returns.write · entrée: returnHandoverPickupSchema
- [ ] `POST /api/return-handovers/:code/handover` — `handOver` (L294) · gardes: NotMerchantGuard · perm: returns.write
- [ ] `POST /api/return-handovers/:code/confirm` — `confirm` (L303) · gardes: NotMerchantGuard · perm: returns.confirm
- [ ] `POST /api/return-handovers/:code/close` — `close` (L310) · gardes: NotMerchantGuard · perm: returns.confirm
- [ ] `POST /api/return-handovers/:code/cancel` — `cancel` (L324) · gardes: NotMerchantGuard · perm: returns.write · entrée: returnHandoverCancelSchema
- [ ] `GET /api/return-handovers/:code/export` — `exportParcels` (L340) · gardes: NotMerchantGuard · perm: returns.read · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/return-handovers/:code/pdf` — `pdf` (L363) · gardes: NotMerchantGuard · perm: returns.read · sortie: `Promise<StreamableFile>`
- [ ] `DELETE /api/return-handovers/:code` — `remove` (L377) · gardes: NotMerchantGuard · perm: returns.write
- [ ] `GET /api/return-slips` — `list` (L443) · perm: returns.read · entrée: slipListQuerySchema
- [ ] `GET /api/return-slips/export` — `export` (L460) · perm: returns.read · entrée: slipListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/return-slips/bulk` — `bulkPreview` (L484) · gardes: NotMerchantGuard · perm: returns.read
- [ ] `POST /api/return-slips/bulk` — `createBulk` (L493) · perm: returns.write
- [ ] `GET /api/return-slips/:code` — `detail` (L500) · perm: returns.read
- [ ] `POST /api/return-slips` — `create` (L507) · perm: returns.write · entrée: returnSlipSchema
- [ ] `GET /api/return-slips/:code/candidates` — `candidates` (L527) · perm: returns.read · entrée: returnCandidatesQuerySchema
- [ ] `POST /api/return-slips/:code/parcels` — `addParcel` (L539) · perm: returns.write · entrée: returnSlipItemSchema
- [ ] `POST /api/return-slips/:code/parcels/batch` — `addParcels` (L558) · perm: returns.write · entrée: returnSlipItemsSchema
- [ ] `DELETE /api/return-slips/:code/parcels/:parcelId` — `removeParcel` (L569) · perm: returns.write
- [ ] `GET /api/return-slips/:code/export` — `exportParcels` (L582) · perm: returns.read · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/return-slips/:code/pdf` — `pdf` (L601) · perm: returns.read · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/return-slips/:code/close` — `close` (L617) · perm: returns.confirm · entrée: returnSlipCloseSchema
- [ ] `DELETE /api/return-slips/:code` — `remove` (L628) · perm: returns.write
- [ ] `GET /api/returns/pending` — `pending` (L699) · perm: returns.read · entrée: pendingQuerySchema
- [ ] `GET /api/returns/pending/export` — `exportPending` (L717) · perm: returns.read · entrée: pendingQuerySchema · sortie: `Promise<StreamableFile>`

## Module 8 — Caisse et chèques

### `modules/courier/courier.controller.ts`

- [ ] `GET /api/courier/me/cheques` — `chequeList` (L130) · gardes: CourierGuard · perm: finance.read
- [ ] `GET /api/courier/me/balance` — `balance` (L147) · gardes: CourierGuard · perm: finance.read
- [ ] `GET /api/courier/me/remittance` — `openRemittance` (L168) · gardes: CourierGuard · perm: finance.read · sortie: `Promise<CourierOpenRemittance>`
- [ ] `POST /api/courier/me/remittance/close` — `closeRemittance` (L189) · gardes: CourierGuard · perm: finance.read · sortie: `Promise<CourierOpenRemittance>`
- [ ] `POST /api/courier/me/cheques` — `cheque` (L294) · gardes: CourierGuard · perm: finance.read · entrée: courierChequeSchema

### `modules/finance/cheque-slip.controller.ts`

- [ ] `POST /api/cheques/slip` — `groupSlip` (L34) · gardes: NotMerchantGuard · perm: finance.read · entrée: chequeGroupSlipSchema · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/cheques/:id/slip` — `slip` (L51) · gardes: NotMerchantGuard · perm: finance.read · sortie: `Promise<StreamableFile>`

### `modules/finance/cheques.controller.ts`

- [ ] `GET /api/cheques` — `list` (L97) · perm: finance.read · entrée: chequeListQuerySchema
- [ ] `GET /api/cheques/export` — `export` (L114) · perm: finance.read · entrée: chequeListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/cheques/:id` — `detail` (L128) · perm: finance.read
- [ ] `POST /api/cheques/bulk-advance` — `bulkAdvance` (L144) · gardes: NotMerchantGuard · perm: finance.read · entrée: chequeBulkSchema
- [ ] `POST /api/cheques/:id/advance` — `advance` (L163) · gardes: NotMerchantGuard · perm: finance.read · entrée: chequeAdvanceSchema
- [ ] `POST /api/cheques/:id/reject` — `reject` (L175) · gardes: NotMerchantGuard · perm: finance.read · entrée: chequeAdvanceSchema
- [ ] `POST /api/cheques/:id/cancel` — `cancel` (L187) · gardes: NotMerchantGuard · perm: finance.read · entrée: chequeAdvanceSchema

### `modules/finance/finance.controller.ts`

- [ ] `GET /api/payments` — `list` (L470) · perm: finance.read · entrée: paymentListQuerySchema
- [ ] `GET /api/payments/export` — `export` (L481) · perm: finance.read · entrée: paymentListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/payments` — `create` (L495) · perm: finance.pay · entrée: paymentSchema
- [ ] `DELETE /api/payments/:id` — `remove` (L504) · perm: finance.pay
- [ ] `GET /api/remittances` — `list` (L588) · gardes: NotMerchantGuard · perm: finance.read · entrée: remittanceListQuerySchema
- [ ] `GET /api/remittances/export` — `export` (L599) · gardes: NotMerchantGuard · perm: finance.read · entrée: remittanceListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/remittances/candidates` — `candidates` (L613) · gardes: NotMerchantGuard · perm: finance.read · entrée: remittanceCandidateQuerySchema
- [ ] `GET /api/remittances/summary` — `remittanceSummary` (L627) · gardes: NotMerchantGuard · perm: finance.read · entrée: remittanceSummaryQuerySchema
- [ ] `GET /api/remittances/:code` — `detail` (L637) · gardes: NotMerchantGuard · perm: finance.read
- [ ] `POST /api/remittances` — `create` (L644) · gardes: NotMerchantGuard · perm: finance.write · entrée: remittanceCreateSchema
- [ ] `POST /api/remittances/:code/parcels` — `addParcels` (L654) · gardes: NotMerchantGuard · perm: finance.write · entrée: remittanceParcelsSchema
- [ ] `DELETE /api/remittances/:code/parcels/:parcelId` — `removeParcel` (L665) · gardes: NotMerchantGuard · perm: finance.write
- [ ] `PATCH /api/remittances/:code/count` — `count` (L678) · gardes: NotMerchantGuard · perm: finance.write · entrée: remittanceCountSchema
- [ ] `GET /api/remittances/:code/export` — `exportParcels` (L694) · gardes: NotMerchantGuard · perm: finance.read · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/remittances/:code/pdf` — `pdf` (L717) · gardes: NotMerchantGuard · perm: finance.read · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/remittances/:code/confirm` — `confirm` (L729) · gardes: NotMerchantGuard · perm: finance.confirm
- [ ] `POST /api/remittances/:code/reopen` — `reopen` (L742) · gardes: NotMerchantGuard · perm: finance.confirm
- [ ] `DELETE /api/remittances/:code` — `remove` (L749) · gardes: NotMerchantGuard · perm: finance.write
- [ ] `GET /api/finance/dashboard` — `get` (L956) · gardes: NotMerchantGuard · perm: finance.read · entrée: dashboardQuerySchema
- [ ] `GET /api/finance/overview` — `overviewGet` (L973) · gardes: NotMerchantGuard · perm: finance.read · entrée: financeOverviewQuerySchema
- [ ] `GET /api/banks` — `listBanks` (L1189) · perm: finance.read
- [ ] `GET /api/bank-accounts` — `listAccounts` (L1198) · gardes: NotMerchantGuard · perm: finance.read
- [ ] `GET /api/couriers/:id/balance` — `balance` (L1220) · gardes: NotMerchantGuard · perm: finance.read
- [ ] `POST /api/couriers/:id/balance/settlements` — `settle` (L1227) · gardes: NotMerchantGuard · perm: finance.pay · entrée: courierSettlementSchema

### `modules/petty-cash/petty-cash-courier.controller.ts`

- [ ] `GET /api/courier/me/petty-cash` — `list` (L93) · gardes: CourierGuard · perm: pettycash.courier · entrée: pettyCashCourierListQuerySchema
- [ ] `GET /api/courier/me/petty-cash/export` — `export` (L109) · gardes: CourierGuard · perm: pettycash.courier · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/courier/me/petty-cash` — `create` (L120) · gardes: CourierGuard · perm: pettycash.courier · entrée: pettyCashCourierExpenseSchema
- [ ] `PATCH /api/courier/me/petty-cash/:id` — `update` (L133) · gardes: CourierGuard · perm: pettycash.courier · entrée: pettyCashCourierExpenseSchema
- [ ] `DELETE /api/courier/me/petty-cash/:id` — `remove` (L145) · gardes: CourierGuard · perm: pettycash.courier · sortie: `Promise<void>`

### `modules/petty-cash/petty-cash.controller.ts`

- [ ] `GET /api/petty-cash` — `list` (L105) · perm: pettycash.read · entrée: pettyCashListFilterSchema
- [ ] `GET /api/petty-cash/meta` — `meta` (L116) · perm: pettycash.read
- [ ] `PUT /api/petty-cash/categories` — `setCategories` (L123) · perm: pettycash.write · entrée: pettyCashCategoriesSchema
- [ ] `GET /api/petty-cash/export` — `export` (L135) · perm: pettycash.read · entrée: pettyCashListFilterSchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/petty-cash/pdf` — `pdf` (L150) · perm: pettycash.read · entrée: pettyCashListFilterSchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/petty-cash/days` — `days` (L167) · perm: pettycash.read · entrée: pettyCashDaysQuerySchema
- [ ] `GET /api/petty-cash/days/:day` — `day` (L177) · perm: pettycash.read · entrée: pettyCashDayParamSchema, pettyCashDayCloseSchema
- [ ] `POST /api/petty-cash/days/:day/close` — `closeDay` (L189) · perm: pettycash.write · entrée: pettyCashDayParamSchema, pettyCashDayCloseSchema
- [ ] `POST /api/petty-cash/days/:day/reopen` — `reopenDay` (L201) · perm: pettycash.write · entrée: pettyCashDayParamSchema, pettyCashDayReopenSchema
- [ ] `GET /api/petty-cash/days/:day/export` — `exportDay` (L214) · perm: pettycash.read · entrée: pettyCashDayParamSchema, pettyCashDayCloseSchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/petty-cash/days/:day/pdf` — `pdfDay` (L233) · perm: pettycash.read · entrée: pettyCashDayParamSchema, pettyCashDayCloseSchema · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/petty-cash` — `create` (L250) · perm: pettycash.write · entrée: pettyCashMovementSchema
- [ ] `PATCH /api/petty-cash/:id` — `update` (L260) · perm: pettycash.write · entrée: pettyCashMovementSchema
- [ ] `DELETE /api/petty-cash/:id` — `remove` (L272) · perm: pettycash.write · sortie: `Promise<void>`
- [ ] `PATCH /api/petty-cash/:id/validate` — `validate` (L284) · perm: pettycash.write
- [ ] `PATCH /api/petty-cash/:id/refuse` — `refuse` (L291) · perm: pettycash.write · entrée: pettyCashRefuseSchema
- [ ] `GET /api/petty-cash/:id/proof` — `proof` (L303) · perm: pettycash.read · sortie: `Promise<StreamableFile>`

## Module 9 — Facturation

### `modules/finance/finance.controller.ts`

- [ ] `GET /api/invoices` — `list` (L193) · perm: finance.read · entrée: invoiceListQuerySchema
- [ ] `GET /api/invoices/export` — `export` (L210) · perm: finance.read · entrée: invoiceListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/invoices/days` — `days` (L228) · perm: finance.read · entrée: invoiceDaysQuerySchema
- [ ] `GET /api/invoices/day-pdf` — `dayPdf` (L240) · perm: finance.read · entrée: invoiceDayPdfQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/invoices/billable` — `billable` (L259) · gardes: NotMerchantGuard · perm: finance.read · entrée: billableQuerySchema
- [ ] `GET /api/invoices/bulk-candidates` — `bulkCandidates` (L283) · perm: finance.write
- [ ] `GET /api/invoices/:code` — `detail` (L290) · perm: finance.read
- [ ] `POST /api/invoices` — `create` (L297) · perm: finance.write · entrée: invoiceCreateSchema
- [ ] `POST /api/invoices/monthly` — `createMonthly` (L315) · perm: finance.write · entrée: invoiceMonthlySchema
- [ ] `POST /api/invoices/bulk` — `createBulk` (L325) · perm: finance.write · entrée: bulkInvoiceSchema
- [ ] `POST /api/invoices/:code/parcels` — `addParcels` (L337) · perm: finance.write · entrée: invoiceParcelsSchema
- [ ] `DELETE /api/invoices/:code/parcels/:parcelId` — `removeParcel` (L348) · perm: finance.write
- [ ] `POST /api/invoices/:code/adjustments` — `addAdjustment` (L364) · perm: finance.validate · entrée: invoiceAdjustmentSchema
- [ ] `DELETE /api/invoices/:code/adjustments/:id` — `removeAdjustment` (L375) · perm: finance.validate
- [ ] `GET /api/invoices/:code/export` — `exportInvoice` (L396) · perm: finance.read · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/invoices/:code/pdf` — `pdf` (L416) · perm: finance.read · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/invoices/:code/issue` — `issue` (L432) · perm: finance.validate · entrée: invoiceIssueSchema
- [ ] `DELETE /api/invoices/:code` — `remove` (L443) · perm: finance.write
- [ ] `GET /api/courier-statements` — `list` (L801) · gardes: NotMerchantGuard · perm: finance.read · entrée: statementListQuerySchema
- [ ] `GET /api/courier-statements/export` — `export` (L812) · gardes: NotMerchantGuard · perm: finance.read · entrée: statementListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/courier-statements/:code` — `detail` (L826) · gardes: NotMerchantGuard · perm: finance.read
- [ ] `GET /api/courier-statements/:code/pdf` — `pdf` (L834) · gardes: NotMerchantGuard · perm: finance.read · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/courier-statements` — `create` (L848) · gardes: NotMerchantGuard · perm: finance.write · entrée: courierStatementSchema
- [ ] `POST /api/courier-statements/:code/recompute` — `recompute` (L858) · gardes: NotMerchantGuard · perm: finance.write
- [ ] `POST /api/courier-statements/:code/validate` — `validate` (L865) · gardes: NotMerchantGuard · perm: finance.validate
- [ ] `POST /api/courier-statements/:code/pay` — `pay` (L874) · gardes: NotMerchantGuard · perm: finance.pay · entrée: statementPaySchema
- [ ] `GET /api/courier-statements/:code/cheque-photo` — `chequePhoto` (L902) · gardes: NotMerchantGuard · perm: finance.read · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/courier-statements/:code/cheque-photo` — `attachChequePhoto` (L919) · gardes: NotMerchantGuard · perm: finance.read · entrée: chequePhotoSchema
- [ ] `DELETE /api/courier-statements/:code` — `remove` (L930) · gardes: NotMerchantGuard · perm: finance.write
- [ ] `GET /api/cod-payouts` — `list` (L1030) · perm: finance.read · entrée: codPayoutListQuerySchema
- [ ] `GET /api/cod-payouts/export` — `export` (L1041) · perm: finance.read · entrée: codPayoutListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/cod-payouts/candidates` — `candidates` (L1056) · perm: finance.write · entrée: codPayoutCandidateQuerySchema
- [ ] `GET /api/cod-payouts/bulk-candidates` — `bulkCandidates` (L1070) · perm: finance.validate · entrée: codPayoutBulkQuerySchema
- [ ] `POST /api/cod-payouts/bulk` — `createBulk` (L1080) · perm: finance.validate · entrée: codPayoutBulkSchema
- [ ] `GET /api/cod-payouts/:code` — `detail` (L1090) · perm: finance.read
- [ ] `GET /api/cod-payouts/:code/export` — `exportParcels` (L1099) · perm: finance.read · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/cod-payouts/:code/pdf` — `pdf` (L1118) · perm: finance.read · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/cod-payouts` — `create` (L1130) · perm: finance.write · entrée: codPayoutCreateSchema
- [ ] `POST /api/cod-payouts/:code/parcels` — `addParcels` (L1140) · perm: finance.write · entrée: codPayoutParcelsSchema
- [ ] `DELETE /api/cod-payouts/:code/parcels/:parcelId` — `removeParcel` (L1151) · perm: finance.write
- [ ] `POST /api/cod-payouts/:code/pay` — `pay` (L1164) · perm: finance.pay · entrée: codPayoutPaySchema
- [ ] `DELETE /api/cod-payouts/:code` — `remove` (L1175) · perm: finance.write

### `modules/finance/pro-invoices.controller.ts`

- [ ] `GET /api/invoices-pro` — `list` (L65) · gardes: NotMerchantGuard · perm: finance.read · entrée: invoiceListQuerySchema
- [ ] `GET /api/invoices-pro/export` — `export` (L76) · gardes: NotMerchantGuard · perm: finance.read · entrée: invoiceListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/invoices-pro/days` — `days` (L91) · gardes: NotMerchantGuard · perm: finance.read · entrée: invoiceDaysQuerySchema
- [ ] `GET /api/invoices-pro/day-pdf` — `dayPdf` (L103) · gardes: NotMerchantGuard · perm: finance.read · entrée: invoiceDayPdfQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/invoices-pro/bulk-candidates` — `bulkCandidates` (L118) · gardes: NotMerchantGuard · perm: finance.write
- [ ] `POST /api/invoices-pro` — `create` (L125) · gardes: NotMerchantGuard · perm: finance.write · entrée: invoiceCreateSchema
- [ ] `POST /api/invoices-pro/monthly` — `createMonthly` (L135) · gardes: NotMerchantGuard · perm: finance.write · entrée: invoiceMonthlySchema
- [ ] `POST /api/invoices-pro/bulk` — `createBulk` (L145) · gardes: NotMerchantGuard · perm: finance.write · entrée: bulkInvoiceSchema

### `modules/finance/transfer-orders.controller.ts`

- [ ] `POST /api/transfer-orders` — `create` (L28) · perm: finance.write · entrée: transferOrderCreateSchema
- [ ] `GET /api/transfer-orders/:code` — `detail` (L38) · perm: finance.write
- [ ] `GET /api/transfer-orders/:code/pdf` — `pdf` (L46) · perm: finance.write · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/transfer-orders/:code/xlsx` — `xlsx` (L59) · perm: finance.write · sortie: `Promise<StreamableFile>`

## Module 10 — Réclamations, notifications, tableaux de bord

### `modules/announcements/announcements.controller.ts`

- [ ] `GET /api/announcements` — `list` (L42) · perm: announcements.write · entrée: announcementFiltersSchema
- [ ] `GET /api/announcements/feed` — `feed` (L56) · query: limit
- [ ] `GET /api/announcements/:id` — `detail` (L62)
- [ ] `GET /api/announcements/:id/image` — `image` (L69) · sortie: `Promise<StreamableFile>`
- [ ] `POST /api/announcements` — `create` (L82) · perm: announcements.write · entrée: announcementSchema
- [ ] `PATCH /api/announcements/:id` — `update` (L92) · perm: announcements.write · entrée: announcementSchema
- [ ] `DELETE /api/announcements/:id` — `remove` (L103) · perm: announcements.write

### `modules/claims/claims.controller.ts`

- [ ] `GET /api/claims` — `list` (L64) · perm: claims.read · entrée: claimListQuerySchema
- [ ] `GET /api/claims/export` — `export` (L80) · perm: claims.read · entrée: claimListQuerySchema · sortie: `Promise<StreamableFile>`
- [ ] `GET /api/claims/counters` — `counters` (L95) · perm: claims.read
- [ ] `GET /api/claims/types` — `listTypes` (L102) · perm: claims.read
- [ ] `POST /api/claims/types` — `createType` (L109) · perm: settings.write · entrée: claimTypeSchema
- [ ] `PATCH /api/claims/types/:id` — `updateType` (L116) · perm: settings.write · entrée: claimTypeSchema
- [ ] `DELETE /api/claims/types/:id` — `removeType` (L127) · perm: settings.write
- [ ] `POST /api/claims` — `create` (L134) · perm: claims.write · entrée: createClaimSchema
- [ ] `GET /api/claims/:code` — `detail` (L141) · perm: claims.read
- [ ] `POST /api/claims/:code/events` — `reply` (L150) · perm: claims.write · entrée: replyClaimSchema
- [ ] `PATCH /api/claims/:code/assignee` — `assign` (L161) · perm: claims.handle · entrée: assignClaimSchema
- [ ] `GET /api/claims/:code/attachments/:id` — `attachment` (L173) · perm: claims.read · sortie: `Promise<StreamableFile>`

### `modules/dashboard/dashboard.controller.ts`

- [ ] `GET /api/dashboard` — `get` (L81) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardFiltersSchema
- [ ] `GET /api/dashboard/series` — `series` (L94) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardSeriesSchema
- [ ] `GET /api/dashboard/couriers` — `couriers` (L105) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardCouriersSchema
- [ ] `GET /api/dashboard/cities` — `cities` (L116) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardCitiesSchema
- [ ] `GET /api/dashboard/clients` — `clients` (L133) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardClientsSchema
- [ ] `GET /api/dashboard/clients/:id` — `clientDetail` (L143) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardFiltersSchema
- [ ] `GET /api/dashboard/finance` — `finance` (L157) · gardes: NotMerchantGuard, StaffGuard · perm: finance.read · entrée: dashboardFiltersSchema
- [ ] `GET /api/dashboard/routes` — `routes` (L167) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardFiltersSchema
- [ ] `GET /api/dashboard/delays` — `delays` (L177) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardFiltersSchema
- [ ] `GET /api/dashboard/alerts` — `alerts` (L187) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardFiltersSchema
- [ ] `GET /api/dashboard/export` — `export` (L204) · gardes: NotMerchantGuard, StaffGuard · perm: parcels.read · entrée: dashboardExportSchema · sortie: `Promise<StreamableFile>`

### `modules/notifications/notifications.controller.ts`

- [ ] `POST /api/me/push-token` — `register` (L48) · entrée: pushTokenSchema
- [ ] `DELETE /api/me/push-token` — `forget` (L55) · entrée: forgetTokenSchema
- [ ] `GET /api/me/notification-feed` — `feed` (L73)
- [ ] `POST /api/me/notification-feed/:id/read` — `markRead` (L81)
- [ ] `POST /api/me/notification-feed/read-all` — `markAllRead` (L89)
- [ ] `GET /api/me/notifications` — `settings` (L95)
- [ ] `PATCH /api/me/notifications` — `setPreference` (L101) · entrée: notificationPreferenceSchema
