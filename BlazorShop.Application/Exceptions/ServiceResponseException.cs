namespace BlazorShop.Application.Exceptions
{
    using BlazorShop.Application.DTOs;

    public sealed class ServiceResponseException : Exception
    {
        public ServiceResponseException(string message, ServiceResponseType responseType)
            : base(message)
        {
            ResponseType = responseType;
        }

        public ServiceResponseType ResponseType { get; }
    }
}