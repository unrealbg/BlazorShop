namespace BlazorShop.Web.Shared.Toast
{
    public static class ToastBuilder
    {
        public static ToastOptions Create(ToastLevel level, string message, string heading = "")
        {
            return new ToastOptions
                       {
                           Level = level,
                           Message = message,
                           Heading = heading
                       };
        }

        public static ToastOptions WithIcon(this ToastOptions options, ToastIcon iconClass)
        {
            options.IconClass = iconClass;
            return options;
        }

        public static ToastOptions WithPosition(this ToastOptions options, ToastPosition position)
        {
            options.Position = position;
            return options;
        }

        public static ToastOptions Persisting(this ToastOptions options)
        {
            options.Persist = false;
            return options;
        }

        public static ToastOptions WithDuration(this ToastOptions options, int duration)
        {
            options.Duration = duration;
            return options;
        }
    }
}
