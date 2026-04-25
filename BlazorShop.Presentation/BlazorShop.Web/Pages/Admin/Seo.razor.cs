namespace BlazorShop.Web.Pages.Admin
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Routing;

    public partial class Seo : IDisposable
    {
        private SeoAdminTab _activeTab = SeoAdminTab.Settings;
        private UpdateSeoSettings _settings = new();
        private bool _isSettingsLoading;
        private bool _isSettingsSaving;
        private string? _settingsError;

        private List<GetSeoRedirect> _redirects = [];
        private bool _isRedirectsLoading;
        private string? _redirectsError;
        private bool _showRedirectDialog;
        private bool _isRedirectSaving;
        private bool _isRedirectDeleting;
        private Guid? _busyRedirectId;
        private string? _busyRedirectAction;
        private UpsertSeoRedirect _redirectForm = CreateDefaultRedirect();
        private Guid? _editingRedirectId;
        private bool _showDeleteRedirectDialog;
        private Guid _redirectToDeleteId;
        private string? _redirectToDeletePath;

        [Inject]
        private ISeoSettingsService SeoSettingsService { get; set; } = default!;

        [Inject]
        private ISeoRedirectService SeoRedirectService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            NavigationManager.LocationChanged += HandleLocationChanged;
            UpdateActiveTabFromCurrentUri();

            await Task.WhenAll(LoadSettingsAsync(), LoadRedirectsAsync());
        }

        protected override void OnParametersSet()
        {
            UpdateActiveTabFromCurrentUri();
        }

        private async Task LoadSettingsAsync()
        {
            _isSettingsLoading = true;
            _settingsError = null;

            var result = await this.SeoSettingsService.GetAsync();
            if (this.QueryFailureNotifier.TryNotifyFailure(result, "SEO Settings"))
            {
                _settings = new UpdateSeoSettings();
                _settingsError = result.Message;
                _isSettingsLoading = false;
                return;
            }

            _settings = result.Data is null ? new UpdateSeoSettings() : MapSettings(result.Data);
            _isSettingsLoading = false;
        }

        private async Task RetryLoadSettingsAsync()
        {
            await LoadSettingsAsync();
        }

        private async Task SaveSettingsAsync()
        {
            if (_isSettingsSaving)
            {
                return;
            }

            _isSettingsSaving = true;

            try
            {
                var result = await this.SeoSettingsService.UpdateAsync(_settings);
                if (result.Success)
                {
                    _settingsError = null;

                    if (result.Payload is not null)
                    {
                        _settings = MapSettings(result.Payload);
                    }
                }

                this.ShowToast(result, "SEO Settings");
            }
            finally
            {
                _isSettingsSaving = false;
            }
        }

        private async Task LoadRedirectsAsync()
        {
            _isRedirectsLoading = true;
            _redirectsError = null;

            var result = await this.SeoRedirectService.GetAllAsync();
            if (this.QueryFailureNotifier.TryNotifyFailure(result, "SEO Redirects"))
            {
                _redirects = [];
                _redirectsError = result.Message;
                _isRedirectsLoading = false;
                return;
            }

            _redirects = (result.Data ?? Array.Empty<GetSeoRedirect>())
                .OrderByDescending(x => x.IsActive)
                .ThenByDescending(x => x.CreatedOn)
                .ToList();

            _isRedirectsLoading = false;
        }

        private async Task RetryLoadRedirectsAsync()
        {
            await LoadRedirectsAsync();
        }

        private void OpenCreateRedirectDialog()
        {
            _editingRedirectId = null;
            _redirectForm = CreateDefaultRedirect();
            _showRedirectDialog = true;
        }

        private void OpenEditRedirectDialog(GetSeoRedirect redirect)
        {
            _editingRedirectId = redirect.Id;
            _redirectForm = new UpsertSeoRedirect
            {
                OldPath = redirect.OldPath,
                NewPath = redirect.NewPath,
                StatusCode = redirect.StatusCode,
                IsActive = redirect.IsActive,
            };
            _showRedirectDialog = true;
        }

        private void CancelRedirectEdit()
        {
            _showRedirectDialog = false;
            _editingRedirectId = null;
            _isRedirectSaving = false;
            _redirectForm = CreateDefaultRedirect();
        }

        private async Task SaveRedirectAsync()
        {
            if (_isRedirectSaving)
            {
                return;
            }

            _isRedirectSaving = true;

            try
            {
            var isUpdate = _editingRedirectId.HasValue;
            var result = isUpdate
                ? await this.SeoRedirectService.UpdateAsync(_editingRedirectId!.Value, _redirectForm)
                : await this.SeoRedirectService.CreateAsync(_redirectForm);

            if (result.Success)
            {
                _showRedirectDialog = false;
                _editingRedirectId = null;
                _redirectForm = CreateDefaultRedirect();
                await LoadRedirectsAsync();
            }

            this.ShowToast(result, "SEO Redirects");
            }
            finally
            {
                _isRedirectSaving = false;
            }
        }

        private async Task DeactivateRedirectAsync(Guid redirectId)
        {
            if (_busyRedirectId.HasValue || _isRedirectSaving || _isRedirectDeleting)
            {
                return;
            }

            _busyRedirectId = redirectId;
            _busyRedirectAction = "Deactivating...";

            try
            {
                var result = await this.SeoRedirectService.DeactivateAsync(redirectId);
                if (result.Success)
                {
                    await LoadRedirectsAsync();
                }

                this.ShowToast(result, "SEO Redirects");
            }
            finally
            {
                _busyRedirectId = null;
                _busyRedirectAction = null;
            }
        }

        private void ConfirmDeleteRedirect(GetSeoRedirect redirect)
        {
            _redirectToDeleteId = redirect.Id;
            _redirectToDeletePath = redirect.OldPath;
            _showDeleteRedirectDialog = true;
        }

        private void CancelDeleteRedirect()
        {
            _showDeleteRedirectDialog = false;
            _redirectToDeleteId = Guid.Empty;
            _redirectToDeletePath = null;
        }

        private async Task DeleteRedirectConfirmedAsync()
        {
            if (_isRedirectDeleting || _redirectToDeleteId == Guid.Empty)
            {
                return;
            }

            _isRedirectDeleting = true;
            _showDeleteRedirectDialog = false;

            try
            {
                var result = await this.SeoRedirectService.DeleteAsync(_redirectToDeleteId);
                if (result.Success)
                {
                    await LoadRedirectsAsync();
                }

                this.ShowToast(result, "SEO Redirects");
            }
            finally
            {
                _isRedirectDeleting = false;
                _redirectToDeleteId = Guid.Empty;
                _redirectToDeletePath = null;
            }
        }

        private void ShowToast<TPayload>(ServiceResponse<TPayload> result, string heading)
        {
            var level = result.Success ? ToastLevel.Success : ToastLevel.Error;
            var icon = result.Success ? ToastIcon.Success : ToastIcon.Error;
            var message = string.IsNullOrWhiteSpace(result.Message)
                ? result.Success ? "Saved successfully." : "Request failed."
                : result.Message;

            this.ToastService.ShowToast(level: level, message: message, heading: heading, iconClass: icon);
        }

        private static UpdateSeoSettings MapSettings(GetSeoSettings settings)
        {
            return new UpdateSeoSettings
            {
                SiteName = settings.SiteName,
                DefaultTitleSuffix = settings.DefaultTitleSuffix,
                DefaultMetaDescription = settings.DefaultMetaDescription,
                DefaultOgImage = settings.DefaultOgImage,
                BaseCanonicalUrl = settings.BaseCanonicalUrl,
                CompanyName = settings.CompanyName,
                CompanyLogoUrl = settings.CompanyLogoUrl,
                CompanyPhone = settings.CompanyPhone,
                CompanyEmail = settings.CompanyEmail,
                CompanyAddress = settings.CompanyAddress,
                FacebookUrl = settings.FacebookUrl,
                InstagramUrl = settings.InstagramUrl,
                XUrl = settings.XUrl,
            };
        }

        private static UpsertSeoRedirect CreateDefaultRedirect()
        {
            return new UpsertSeoRedirect
            {
                StatusCode = SeoValidationConstraints.PermanentRedirectStatusCode,
                IsActive = true,
            };
        }

        private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
        {
            UpdateActiveTabFromUri(args.Location);
            StateHasChanged();
        }

        private void UpdateActiveTabFromCurrentUri()
        {
            UpdateActiveTabFromUri(NavigationManager.Uri);
        }

        private void UpdateActiveTabFromUri(string uri)
        {
            var relativePath = NavigationManager.ToBaseRelativePath(uri)
                .Split('?', '#')[0]
                .TrimEnd('/');

            _activeTab = string.Equals(relativePath, "admin/redirects", StringComparison.OrdinalIgnoreCase)
                ? SeoAdminTab.Redirects
                : SeoAdminTab.Settings;
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= HandleLocationChanged;
        }

        private enum SeoAdminTab
        {
            Settings,
            Redirects,
        }
    }
}
