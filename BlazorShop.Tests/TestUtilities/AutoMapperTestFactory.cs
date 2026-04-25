namespace BlazorShop.Tests.TestUtilities
{
    using AutoMapper;

    using BlazorShop.Application.Mapping;

    using Microsoft.Extensions.Logging;

    public static class AutoMapperTestFactory
    {
        public static IMapper CreateMapper()
        {
            var loggerFactory = LoggerFactory.Create(_ => { });
            var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingConfig>(), loggerFactory);
            return configuration.CreateMapper();
        }
    }
}