namespace BlazorShop.Web.Components.Shared
{
    using Microsoft.AspNetCore.Components;

    public partial class ModalDialog
    {
        [Parameter]
        public bool IsVisible { get; set; }

        [Parameter]
        public EventCallback<bool> IsVisibleChanged { get; set; }

        [Parameter]
        public RenderFragment? Header { get; set; }

        [Parameter]
        public RenderFragment? Body { get; set; }

        [Parameter]
        public RenderFragment? Footer { get; set; }

        [Parameter]
        public EventCallback OnClose { get; set; }

        private async Task Close()
        {
            IsVisible = false;
            await IsVisibleChanged.InvokeAsync(IsVisible);
            await OnClose.InvokeAsync();
        }
    }
}