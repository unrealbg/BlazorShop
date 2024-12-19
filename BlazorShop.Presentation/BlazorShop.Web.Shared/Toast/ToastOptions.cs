namespace BlazorShop.Web.Shared.Toast
{
    public class ToastOptions
    {
        public ToastLevel Level { get; set; }

        public string Message { get; set; } = string.Empty;

        public string Heading { get; set; } = string.Empty;

        public ToastIcon IconClass { get; set; } = ToastIcon.Default;

        public ToastPosition Position { get; set; } = ToastPosition.TopRight;

        public bool Persist { get; set; } = false;

        public int Duration { get; set; } = 5000;
    }
}
