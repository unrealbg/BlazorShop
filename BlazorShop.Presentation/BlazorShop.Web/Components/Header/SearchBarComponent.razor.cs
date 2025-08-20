namespace BlazorShop.Web.Components.Header
{
    using System.Linq;
    using BlazorShop.Web.Shared.Models.Product;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web;

    public partial class SearchBarComponent
    {
        [Parameter]
        public IEnumerable<GetProduct> Products { get; set; } = Enumerable.Empty<GetProduct>();

        private string _query = string.Empty;
        private List<GetProduct> _matches = new();
        private bool _isOpen;
        private int _activeIndex = -1;

        private void OnInput(ChangeEventArgs e)
        {
            _query = e.Value?.ToString() ?? string.Empty;
            Filter();
            _isOpen = _matches.Count > 0;
        }

        private void Filter()
        {
            if (string.IsNullOrWhiteSpace(_query))
            {
                _matches.Clear();
                _activeIndex = -1;
                return;
            }

            var q = _query.Trim();
            _matches = Products
                .Where(x => (x.Name?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false)
                         || (x.Description?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false))
                .Take(8)
                .ToList();

            _activeIndex = _matches.Count > 0 ? 0 : -1;
        }

        private async Task OnBlur()
        {
            await Task.Delay(120);
            _isOpen = false;
            StateHasChanged();
        }

        private void OnFocus() { if (_matches.Count > 0) _isOpen = true; }

        private void OnKeyDown(KeyboardEventArgs e)
        {
            if (!_isOpen && (e.Key == "ArrowDown" || e.Key == "ArrowUp"))
            {
                _isOpen = _matches.Count > 0;
            }

            switch (e.Key)
            {
                case "ArrowDown":
                    if (_matches.Count > 0)
                    {
                        _activeIndex = (_activeIndex + 1) % _matches.Count;
                    }
                    break;
                case "ArrowUp":
                    if (_matches.Count > 0)
                    {
                        _activeIndex = (_activeIndex - 1 + _matches.Count) % _matches.Count;
                    }
                    break;
                case "Enter":
                    if (_activeIndex >= 0 && _activeIndex < _matches.Count)
                    {
                        SelectSuggestion(_activeIndex);
                    }
                    else
                    {
                        NavigateToQuery();
                    }
                    break;
                case "Escape":
                    _isOpen = false;
                    break;
            }
        }

        private void SelectSuggestion(int index)
        {
            if (index < 0 || index >= _matches.Count) return;
            var p = _matches[index];
            _query = p.Name ?? _query;
            _isOpen = false;
            this.NavigationManager.NavigateTo($"search-result/{Uri.EscapeDataString(_query)}");
        }

        private void NavigateToQuery()
        {
            var q = _query?.Trim();
            if (!string.IsNullOrWhiteSpace(q))
            {
                this.NavigationManager.NavigateTo($"search-result/{Uri.EscapeDataString(q)}");
            }
        }
    }
}