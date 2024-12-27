namespace BlazorShop.Application.DTOs
{
    public class EmailSettings
    {
        public string From { get; set; }          

        public string DisplayName { get; set; }  

        public string SmtpServer { get; set; } 

        public int Port { get; set; }           

        public bool UseSsl { get; set; }      

        public string Username { get; set; } 

        public string Password { get; set; }
    }
}
