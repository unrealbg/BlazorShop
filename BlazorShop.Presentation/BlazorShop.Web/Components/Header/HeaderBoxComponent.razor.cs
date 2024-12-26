namespace BlazorShop.Web.Components.Header
{
    using System.Security.Claims;

    using BlazorShop.Web.Shared;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Authorization;

    public partial class HeaderBoxComponent
    {
        private bool _isAdmin;
        private bool _isUser;
        
        [CascadingParameter]
        private Task<AuthenticationState> AuthState { get; set; } = default!;


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
        }
    }
}