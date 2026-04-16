namespace BlazorShop.Web.Components.Header
{
    using BlazorShop.Web.Shared.Models.Product;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web;

    public partial class SearchBarComponent
    {
        private string _query = string.Empty;
        private List<GetCatalogProduct> _matches = new();
        private bool _isOpen;
        private int _activeIndex = -1;
        private CancellationTokenSource? _searchCts;

        private async Task OnInput(ChangeEventArgs e)
        {
            _query = e.Value?.ToString() ?? string.Empty;
            await FilterAsync();
        }

        private async Task FilterAsync()
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();

            if (string.IsNullOrWhiteSpace(_query) || _query.Trim().Length < 2)
            {
                _matches.Clear();
                _activeIndex = -1;
                _isOpen = false;
                return;
            }

            var token = _searchCts.Token;
            var q = _query.Trim();

            try
            {
                await Task.Delay(150, token);

                var result = await this.ProductService.GetCatalogPageAsync(new ProductCatalogQuery
                {
                    PageNumber = 1,
                    PageSize = 8,
                    SearchTerm = q,
                    SortBy = ProductCatalogSortBy.NameAscending,
                });

                if (token.IsCancellationRequested)
                {
                    return;
                }

                _matches = result.Success
                    ? (result.Data?.Items ?? []).ToList()
                    : [];
                _activeIndex = _matches.Count > 0 ? 0 : -1;
                _isOpen = _matches.Count > 0;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                _matches.Clear();
                _activeIndex = -1;
                _isOpen = false;
            }
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