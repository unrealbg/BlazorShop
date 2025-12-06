namespace BlazorShop.Application.DTOs
{
    public class EmailSettings
    {
        public string From { get; set; } = string.Empty;         

        public string DisplayName { get; set; } = string.Empty; 

        public string SmtpServer { get; set; } = string.Empty;

        public int Port { get; set; }           

        public bool UseSsl { get; set; }      

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
