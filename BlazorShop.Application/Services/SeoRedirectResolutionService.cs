namespace BlazorShop.Application.Services
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts.Seo;

    public class SeoRedirectResolutionService : ISeoRedirectResolutionService
    {
        private const int MaxRedirectHops = 10;

        private readonly ISeoRedirectRepository _seoRedirectRepository;

        public SeoRedirectResolutionService(ISeoRedirectRepository seoRedirectRepository)
        {
            _seoRedirectRepository = seoRedirectRepository;
        }

        public async Task<SeoRedirectResolutionDto?> ResolvePublicPathAsync(string? path)
        {
            var normalizedPath = SeoRedirectPathUtility.NormalizePath(path);
            if (!SeoRedirectPathUtility.IsRootRelativePath(normalizedPath))
            {
                return null;
            }

            var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentPath = normalizedPath!;
            Domain.Entities.SeoRedirect? firstRedirect = null;

            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                if (!visitedPaths.Add(currentPath))
                {
                    return null;
                }

                var redirect = await _seoRedirectRepository.GetActiveByOldPathAsync(currentPath);
                if (redirect is null)
                {
                    return firstRedirect is null || SeoRedirectPathUtility.PathsEqual(normalizedPath, currentPath)
                        ? null
                        : new SeoRedirectResolutionDto
                        {
                            NewPath = currentPath,
                            StatusCode = firstRedirect.StatusCode,
                        };
                }

                if (!SeoRedirectPathUtility.IsRootRelativePath(redirect.NewPath)
                    || SeoRedirectPathUtility.PathsEqual(currentPath, redirect.NewPath))
                {
                    return null;
                }

                firstRedirect ??= redirect;
                currentPath = redirect.NewPath!;
            }

            return null;
        }
    }
}