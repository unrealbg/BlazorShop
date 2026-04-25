namespace BlazorShop.Application.Services
{
    using System.Text.Json;

    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Entities;

    public class CategoryService : ICategoryService
    {
        private readonly IGenericRepository<Category> _genericRepository;
        private readonly IMapper _mapper;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAdminAuditService? _auditService;

        public CategoryService(IGenericRepository<Category> genericRepository, IMapper mapper, ICategoryRepository categoryRepository, IAdminAuditService? auditService = null)
        {
            _genericRepository = genericRepository;
            _mapper = mapper;
            _categoryRepository = categoryRepository;
            _auditService = auditService;
        }

        public async Task<IEnumerable<GetCategory>> GetAllAsync()
        {
            var result = await _genericRepository.GetAllAsync();
            return result.Any() ? _mapper.Map<IEnumerable<GetCategory>>(result) : new List<GetCategory>();
        }

        public async Task<GetCategory> GetByIdAsync(Guid id)
        {
            var result = await _genericRepository.GetByIdAsync(id);
            return result != null ? _mapper.Map<GetCategory>(result) : new GetCategory();
        }

        public async Task<ServiceResponse> AddAsync(CreateCategory category)
        {
            var entity = _mapper.Map<Category>(category);
            int result = await _genericRepository.AddAsync(entity);

            if (result <= 0)
            {
                return new ServiceResponse(false, "Category not added");
            }

            await LogAsync("Category.Created", entity.Id, $"Category {entity.Name} created.", new { entity.Name });
            return new ServiceResponse(true, "Category added successfully");
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateCategory category)
        {
            var existingCategory = await _genericRepository.GetByIdAsync(category.Id);

            if (existingCategory is null)
            {
                return new ServiceResponse(false, "Category not found");
            }

            _mapper.Map(category, existingCategory);
            int result = await _genericRepository.UpdateAsync(existingCategory);

            if (result <= 0)
            {
                return new ServiceResponse(false, "Category not found");
            }

            await LogAsync("Category.Updated", existingCategory.Id, $"Category {existingCategory.Name} updated.", new { existingCategory.Name });
            return new ServiceResponse(true, "Category updated successfully");
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var existingCategory = await _genericRepository.GetByIdAsync(id);
            var result = await _genericRepository.DeleteAsync(id);

            if (result <= 0)
            {
                return new ServiceResponse(false, "Category not found");
            }

            await LogAsync("Category.Deleted", id, $"Category {existingCategory?.Name ?? id.ToString()} deleted.", new { existingCategory?.Name });
            return new ServiceResponse(true, "Category deleted successfully");
        }

        public async Task<IEnumerable<GetProduct>> GetProductsByCategoryAsync(Guid id)
        {
            var result = await _categoryRepository.GetProductsByCategoryAsync(id);
            return result.Any() ? _mapper.Map<IEnumerable<GetProduct>>(result) : [];
        }

        private async Task LogAsync(string action, Guid entityId, string summary, object metadata)
        {
            if (_auditService is null)
            {
                return;
            }

            await _auditService.LogAsync(new CreateAdminAuditLogDto
            {
                Action = action,
                EntityType = "Category",
                EntityId = entityId.ToString(),
                Summary = summary,
                MetadataJson = JsonSerializer.Serialize(metadata),
            });
        }
    }
}
