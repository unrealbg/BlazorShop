namespace BlazorShop.Web.Shared.Models
{
    public class ApiCall
    {
        public string? Type { get; set; }

        public HttpClient? Client { get; set; }

        public string? Route { get; set; }

        public dynamic? Model { get; set; }

        public string? Id { get; set; }

        public void ToString(Guid id) => this.Id = id.ToString();
    }
}
