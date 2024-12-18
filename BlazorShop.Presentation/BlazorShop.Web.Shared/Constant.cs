namespace BlazorShop.Web.Shared
{
    public static class Constant
    {
        public static class Product
        {
            public const string GetAll = "product/all";
            public const string Get = "product/single";
            public const string Add = "product/add";
            public const string Update = "product/update";
            public const string Delete = "product/delete";
        }

        public static class Category
        {
            public const string GetAll = "category/all";
            public const string GetProductByCategory = "category/products-by-category";
            public const string Get = "category/single";
            public const string Add = "category/add";
            public const string Update = "category/update";
            public const string Delete = "category/delete";
        }

        public static class Authentication
        {
            public const string Type = "Bearer";
            public const string Register = "authentication/register";
            public const string Login = "authentication/login";
            public const string ReviveToke = "authentication/refreshToken";
        }

        public static class ApiCallType
        {
            public const string Get = "get";
            public const string Post = "post";
            public const string Delete = "delete";
            public const string Update = "update";
        }

        public static class Cookie
        {
            public const string Name = "token";
            public const string Path = "/";
            public const int Days = 1;
        }

        public static class ApiClient
        {
            public const string Name = "Blazor-Client";
        }

        public static class Payment
        {
            public const string GetAll = "payment/methods";
        }

        public static class Cart
        {
            public const string Checkout = "cart/checkout";
            public const string SaveCart = "cart/save-checkout";
            public const string Name = "my-cart";
        }
    }
}
