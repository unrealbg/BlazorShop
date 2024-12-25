namespace BlazorShop.Web.Components.Shared
{
    using Microsoft.AspNetCore.Components;

    public partial class ModalDialog
    {
        private bool _isVisible;

        [Parameter]
        public bool IsVisible
        {
            get => this._isVisible;
            set
            {
                if (this._isVisible != value)
                {
                    this._isVisible = value;
                    this.IsVisibleChanged.InvokeAsync(value);
                }
            }
        }

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

        private void Close()
        {
            this.IsVisible = false;
            this.OnClose.InvokeAsync();
        }
    }
}