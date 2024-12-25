namespace BlazorShop.Web.Pages.Public
{
    public partial class CustomerService
    {
        private string CustomerName { get; set; } = string.Empty;

        private string CustomerEmail { get; set; } = string.Empty;

        private string CustomerMessage { get; set; } = string.Empty;

        private void SubmitTicket()
        {
            // Логика за обработка на заявката
            Console.WriteLine($"Name: {this.CustomerName}, Email: {this.CustomerEmail}, Message: {this.CustomerMessage}");
            this.CustomerName = string.Empty;
            this.CustomerEmail = string.Empty;
            this.CustomerMessage = string.Empty;
        }
    }
}