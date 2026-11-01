param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [switch]$CoordinationOnly
)

$ErrorActionPreference = 'Stop'

$expectedTests = @(
    'BlazorShop.E2E.StorefrontCatalogTests.CatalogSearch_NativeGetResponsiveImagesKeyboardAndSeoWithoutJavaScript',
    'BlazorShop.E2E.StorefrontCatalogTests.CatalogSearch_PaginationLegacySearchAndCommerceHandoffs',
    'BlazorShop.E2E.StorefrontCheckoutTests.HomeCategoryAndProductNavigation_ShowsSeededCatalogContent',
    'BlazorShop.E2E.StorefrontCheckoutTests.Cart_AddsProductAndVariantAndPersistsQuantityAfterReload',
    'BlazorShop.E2E.StorefrontCheckoutTests.CheckoutStart_AnonymousLoginThenAuthenticatedCheckoutPreservesCart',
    'BlazorShop.E2E.StorefrontVisualTests.StorefrontFoundation_ResponsiveNavigationAndCommerce',
    'BlazorShop.E2E.StorefrontListingTests.ListingPages_ResponsiveImagesNavigationAndVariantCommerce',
    'BlazorShop.E2E.StorefrontCheckoutTests.Cart_ResponsiveVariantsRemovalClearAndInvalidStates',
    'BlazorShop.E2E.StorefrontCheckoutTests.CartCookie_InvalidInputRecoveryAndCheckoutHandoff',
    'BlazorShop.E2E.CustomerAccountTests.AccessForms_ResponsiveValidationRegistrationAndConfirmation',
    'BlazorShop.E2E.CustomerAccountTests.CustomerAccount_ResponsiveOrdersProfilePasswordAndNotifications',
    'BlazorShop.E2E.CustomerAccountTests.CustomerOrders_LoadingEmptyErrorAndRetry',
    'BlazorShop.E2E.CustomerAccountTests.AdminAndDemo_LoginRefreshLogoutAndSessionIsolation',
    'BlazorShop.E2E.AdminOperationsTests.AdminScreens_ResponsiveNavigationAndData',
    'BlazorShop.E2E.AdminOperationsTests.AdminProducts_UploadCrudVariantsAndSeo',
    'BlazorShop.E2E.AdminOperationsTests.AdminCategories_CrudSeoAndConfirmation',
    'BlazorShop.E2E.AdminOperationsTests.AdminInventory_PaginationAuthorityAndConflict',
    'BlazorShop.E2E.AdminOperationsTests.AdminDashboardAndOrders_MetricsExportAndTracking',
    'BlazorShop.E2E.AdminOperationsTests.AdminUsers_SearchRolesLockAndAudit',
    'BlazorShop.E2E.AdminOperationsTests.AdminAuthorization_CustomerDeniedAndDemoSessionBound',
    'BlazorShop.E2E.AdminOperationsTests.AdminSeo_MetadataAndRedirectCrud',
    'BlazorShop.E2E.AdminOperationsTests.AdminSettings_StoreOrdersNotificationsAndReadOnlySystem',
    'BlazorShop.E2E.AdminOperationsTests.AdminFeedback_LoadingEmptyFailureAndRetry',
    'BlazorShop.E2E.CheckoutPaymentTests.CheckoutAndPublicPages_ResponsivePresentation',
    'BlazorShop.E2E.CheckoutPaymentTests.CheckoutMethods_RealCodBankTransferAndDoubleSubmitGuard',
    'BlazorShop.E2E.CheckoutPaymentTests.CheckoutFeedback_LoadingUnavailableInvalidCartAndApiRecovery',
    'BlazorShop.E2E.CheckoutPaymentTests.PaymentReturns_QueryParametersCannotMutateOrdersOrInventory',
    'BlazorShop.E2E.CheckoutPaymentTests.PublicInformation_StaticSsrSeoAndKeyboardWithoutJavaScript',
    'BlazorShop.E2E.CheckoutPaymentTests.CheckoutStripe_SyntheticRedirectRetainsPendingPresentation'
)

$coordinationTests = @(
    'BlazorShop.E2E.RefreshCoordinationTests.ProfileNotification_UpdatesPeerClaimsAndDeduplicatesTheSameEvent',
    'BlazorShop.E2E.RefreshCoordinationTests.TwoTabs_ConcurrentBootstrapSerializesCookieRotation',
    'BlazorShop.E2E.RefreshCoordinationTests.TwoTabs_Concurrent401UsesLatestCookieWithoutReplay',
    'BlazorShop.E2E.RefreshCoordinationTests.OneTab_401BurstSharesOneRefreshAndIgnoresDuplicateEvents',
    'BlazorShop.E2E.RefreshCoordinationTests.LogoutDuringCommittedRefresh_DiscardsLateResponseAndSurvivesReload',
    'BlazorShop.E2E.RefreshCoordinationTests.LoginDuringRefresh_AccountSwitchWinsOverLateResponse',
    'BlazorShop.E2E.RefreshCoordinationTests.LostHttpResponse_RequiresLoginAndDoesNotRetryAfterReload',
    'BlazorShop.E2E.RefreshCoordinationTests.TabClosesWithUncertainRefresh_AnotherTabRequiresLoginWithoutPost',
    'BlazorShop.E2E.RefreshCoordinationTests.RefreshRedirect_IsRejectedWithoutRepostingCookie',
    'BlazorShop.E2E.RefreshCoordinationTests.UnavailableWebLocks_FailsClosedWithoutCredentialPost',
    'BlazorShop.E2E.RefreshCoordinationTests.LockTimeoutAndTabInterruption_ReleaseWithoutExecutingStaleCallback'
)
if ($CoordinationOnly) { $expectedTests = $coordinationTests } else { $expectedTests += $coordinationTests }

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "E2E TRX report was not created: $Path"
}

[xml]$trx = Get-Content -LiteralPath $Path -Raw
$namespace = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
$namespace.AddNamespace('trx', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')

$results = @($trx.SelectNodes('//trx:UnitTestResult', $namespace))
if ($results.Count -eq 0) {
    throw 'The E2E run discovered zero tests.'
}

$failed = @($results | Where-Object { $_.outcome -ne 'Passed' })
if ($failed.Count -gt 0) {
    throw "The E2E report contains $($failed.Count) non-passing test result(s)."
}

$actualTests = @($results | ForEach-Object { [string]$_.testName } | Sort-Object -Unique)
$missingTests = @($expectedTests | Where-Object { $_ -notin $actualTests })
$unexpectedTests = @($actualTests | Where-Object { $_ -notin $expectedTests })

if ($missingTests.Count -gt 0) {
    throw "Mandatory E2E scenarios are missing: $($missingTests -join ', ')"
}

if ($unexpectedTests.Count -gt 0 -or $actualTests.Count -ne $expectedTests.Count) {
    throw "Expected exactly $($expectedTests.Count) E2E scenarios, found $($actualTests.Count): $($actualTests -join ', ')"
}

Write-Host "Verified $($actualTests.Count) mandatory passing E2E scenarios in $Path."
