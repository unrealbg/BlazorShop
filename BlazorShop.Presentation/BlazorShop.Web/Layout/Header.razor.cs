namespace BlazorShop.Web.Layout
{
    public partial class Header
    {
        private bool _showCategories;

        private void ToggleCategories() => _showCategories = !_showCategories;
    }
}