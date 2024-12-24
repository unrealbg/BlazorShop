namespace BlazorShop.Web.Components.Notifications
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Toast;

    public partial class Toast : IDisposable
    {
        private List<ToastModel> _toasts = new();

        protected override void OnInitialized()
        {
            this.ToastService.OnShow += this.ShowToast;
        }

        private async void ShowToast(object? sender, ToastEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Message) || string.IsNullOrEmpty(e.Heading))
            {
                return;
            }

            var existingToast = this._toasts.FirstOrDefault(
                t => t.Heading == e.Heading && t.Message == e.Message && t.ToastLevel == e.Level.ToString()); ;

            if (existingToast != null)
            {
                existingToast.CancellationTokenSource.Cancel();
                existingToast.CancellationTokenSource = new CancellationTokenSource();
                await this.DelayToastDismissal(existingToast);
            }
            else
            {
                var newToast = new ToastModel
                {
                    Heading = e.Heading,
                    Message = e.Message,
                    ToastLevel = e.Level.ToString(),
                    Icon = e.IconClass,
                    Position = e.Position,
                    Persist = e.Persist,
                    Duration = e.Duration
                };

                this._toasts.Add(newToast);
                await this.InvokeAsync(this.StateHasChanged);
                await this.DelayToastDismissal(newToast);
            }
        }

        private async Task DelayToastDismissal(ToastModel toast)
        {
            if (toast.Persist)
            {
                return;
            }

            try
            {
                await Task.Delay(toast.Duration, toast.CancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (this._toasts.Contains(toast))
            {
                this._toasts.Remove(toast);
                await this.InvokeAsync(this.StateHasChanged);
            }
        }

        private void RemoveToast(ToastModel toast)
        {
            if (this._toasts.Contains(toast))
            {
                toast.CancellationTokenSource.Cancel();
                this._toasts.Remove(toast);
                this.InvokeAsync(this.StateHasChanged);
            }
        }

        //private string GetToastPosition(ToastModel toast)
        //{
        //    return toast.Position switch
        //        {
        //            ToastPosition.TopRight => ToastPositions.TopRight,
        //            ToastPosition.TopLeft => ToastPositions.TopLeft,
        //            ToastPosition.BottomRight => ToastPositions.BottomRight,
        //            ToastPosition.BottomLeft => ToastPositions.BottomLeft,
        //            _ => ToastPositions.Default
        //        };
        //}

        private string GetToastIcon(ToastModel toast)
        {
            return toast.Icon switch
            {
                ToastIcon.Success => ToastIcons.Success,
                ToastIcon.Error => ToastIcons.Error,
                ToastIcon.Warning => ToastIcons.Warning,
                ToastIcon.Info => ToastIcons.Info,
                _ => ToastIcons.Default
            };
        }

        public void Dispose()
        {
            this.ToastService.OnShow -= this.ShowToast;

            foreach (var toast in this._toasts)
            {
                toast.CancellationTokenSource.Cancel();
            }
        }
    }
}
