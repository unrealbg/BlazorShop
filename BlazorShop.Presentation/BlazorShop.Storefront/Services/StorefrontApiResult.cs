namespace BlazorShop.Storefront.Services
{
    public sealed class StorefrontApiResult<T>
    {
        private StorefrontApiResult(T? value, bool isSuccess, bool isNotFound, bool isServiceUnavailable)
        {
            Value = value;
            IsSuccess = isSuccess;
            IsNotFound = isNotFound;
            IsServiceUnavailable = isServiceUnavailable;
        }

        public T? Value { get; }

        public bool IsSuccess { get; }

        public bool IsNotFound { get; }

        public bool IsServiceUnavailable { get; }

        public static StorefrontApiResult<T> Success(T value)
        {
            return new(value, isSuccess: true, isNotFound: false, isServiceUnavailable: false);
        }

        public static StorefrontApiResult<T> NotFound()
        {
            return new(default, isSuccess: false, isNotFound: true, isServiceUnavailable: false);
        }

        public static StorefrontApiResult<T> ServiceUnavailable()
        {
            return new(default, isSuccess: false, isNotFound: false, isServiceUnavailable: true);
        }
    }
}