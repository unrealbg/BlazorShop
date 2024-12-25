namespace BlazorShop.Web.Shared.Toast
{
    public class ToastEventArgs : EventArgs
    {
        public ToastLevel Level { get; }

        public string Message { get; }

        public string Heading { get; }

        public ToastIcon IconClass { get; }

        public ToastPosition Position { get; }

        public bool Persist { get; }

        public int Duration { get; }

        public ToastEventArgs(ToastLevel level, 
                              string message, 
                              string heading, 
                              ToastIcon iconClass = ToastIcon.Default,
                              ToastPosition position = ToastPosition.TopRight,
                              bool persist = false, 
                              int duration = 5000)
        {
            this.Level = level;
            this.Message = message;
            this.Heading = heading;
            this.IconClass = iconClass;
            this.Position = position;
            this.Persist = persist;
            this.Duration = duration;
        }
    }
}
