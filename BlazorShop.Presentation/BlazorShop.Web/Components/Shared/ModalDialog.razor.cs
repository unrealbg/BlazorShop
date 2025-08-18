namespace BlazorShop.Web.Components.Shared
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web;

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

        [Parameter]
        public bool CloseOnBackdrop { get; set; } = true;

        [Parameter]
        public bool ShowCloseButton { get; set; } = true;

        // sm, md, lg, xl
        [Parameter]
        public string Size { get; set; } = "md";

        private async Task Close()
        {
            IsVisible = false;
            await IsVisibleChanged.InvokeAsync(IsVisible);
            await OnClose.InvokeAsync();
        }

        private async Task OnBackdropClick()
        {
            if (CloseOnBackdrop)
            {
                await Close();
            }
        }

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Escape")
            {
                await Close();
            }
        }

        private string GetMaxWidth() => Size switch
        {
            "sm" => "max-w-md",
            "md" => "max-w-lg",
            "lg" => "max-w-2xl",
            "xl" => "max-w-4xl",
            _ => "max-w-lg"
        };
    }
}