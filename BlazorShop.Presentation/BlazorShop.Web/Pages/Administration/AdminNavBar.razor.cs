namespace BlazorShop.Web.Pages.Administration
{
    public partial class AdminNavBar
    {
        private bool IsMenuVisible = false;

        private void ToggleMenu()
        {
            this.IsMenuVisible = !this.IsMenuVisible;
        }

        private void HideMenu()
        {
            this.IsMenuVisible = false;
        }
    }
}