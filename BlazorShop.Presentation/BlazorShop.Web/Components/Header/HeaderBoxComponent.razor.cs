namespace BlazorShop.Web.Components.Header
{
    using System.Security.Claims;

    using BlazorShop.Web.Shared;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Authorization;

    public partial class HeaderBoxComponent
    {
        [CascadingParameter]
        private Task<AuthenticationState> AuthState { get; set; } = default!;

        private bool IsAdmin;

        private string UserEmail = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            var user = (await this.AuthState).User;

            this.IsAdmin = user.IsInRole(Constant.Administration.AdminRole);

            var emailClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            if (emailClaim != null)
            {
                this.UserEmail = emailClaim.Value;
            }
        }
    }
}