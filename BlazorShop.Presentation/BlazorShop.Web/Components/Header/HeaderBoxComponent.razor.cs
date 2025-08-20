namespace BlazorShop.Web.Components.Header
{
    using System.Security.Claims;
    using System.Text.Json;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.CookieStorage.Contracts;
    using BlazorShop.Web.Shared.Models.Payment;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Authorization;
    using Microsoft.JSInterop;

    public partial class HeaderBoxComponent : IAsyncDisposable
    {
        private bool _isAdmin;
        private bool _isUser;
        private int _cartCount;

        [CascadingParameter]
        private Task<AuthenticationState> AuthState { get; set; } = default!;

        [Inject]
        private IJSRuntime Js { get; set; } = default!;

        private IJSObjectReference? _module;
        private DotNetObjectReference<HeaderBoxComponent>? _selfRef;

        private string UserEmail = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            var user = (await this.AuthState).User;

            _isAdmin = user.IsInRole(Constant.Administration.AdminRole);
            _isUser = user.Identity?.IsAuthenticated ?? false;

            var emailClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            if (emailClaim != null)
            {
                this.UserEmail = emailClaim.Value;
            }

            await LoadCartCountAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _module = await Js.InvokeAsync<IJSObjectReference>("import", "./js/cartBadge.js");
                _selfRef = DotNetObjectReference.Create(this);
                if (_module is not null)
                {
                    try
                    {
                        var cartJson = await _module.InvokeAsync<string?>("getCartCookie");
                        if (cartJson is not null)
                        {
                            UpdateCountFromJson(cartJson);
                            await InvokeAsync(StateHasChanged);
                        }
                    }
                    catch { }

                    await _module.InvokeVoidAsync("subscribeCartChanges", _selfRef);
                }
            }
        }

        private void UpdateCountFromJson(string json)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<ProcessCart>>(json) ?? new();
                _cartCount = items
                    .Select(i => new { i.ProductId, i.VariantId })
                    .Distinct()
                    .Count();
            }
            catch
            {
                _cartCount = 0;
            }
        }

        [JSInvokable]
        public async Task RefreshCartCount()
        {
            await LoadCartCountAsync();
            await InvokeAsync(StateHasChanged);
        }

        private async Task LoadCartCountAsync()
        {
            try
            {
                var cartJson = await CookieStorageService.GetAsync(Constant.Cart.Name);
                if (string.IsNullOrWhiteSpace(cartJson))
                {
                    _cartCount = 0;
                    return;
                }
                UpdateCountFromJson(cartJson);
            }
            catch
            {
                _cartCount = 0;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_module is not null)
                {
                    await _module.InvokeVoidAsync("unsubscribeCartChanges");
                    await _module.DisposeAsync();
                }
            }
            catch { }
            _selfRef?.Dispose();
        }
    }
}