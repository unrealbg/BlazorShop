namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Entities;

    public class CategoryService : ICategoryService
    {
        private readonly IGenericRepository<Category> _genericRepository;
        private readonly IMapper _mapper;
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(IGenericRepository<Category> genericRepository, IMapper mapper, ICategoryRepository categoryRepository)
        {
            _genericRepository = genericRepository;
            _mapper = mapper;
            _categoryRepository = categoryRepository;
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
            int result = await _genericRepository.AddAsync(_mapper.Map<Category>(category));

            return result > 0 ? new ServiceResponse(true, "Category added successfully") : new ServiceResponse(false, "Category not added");
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateCategory category)
        {
            int result = await _genericRepository.UpdateAsync(_mapper.Map<Category>(category));
            return result > 0 ? new ServiceResponse(true, "Category updated successfully") : new ServiceResponse(false, "Category not found");
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var result = await _genericRepository.DeleteAsync(id);

            return result > 0 ? new ServiceResponse(true, "Category deleted successfully") : new ServiceResponse(false, "Category not found");
        }

        public async Task<IEnumerable<GetProduct>> GetProductsByCategoryAsync(Guid id)
        {
            var result = await _categoryRepository.GetProductsByCategoryAsync(id);
            return result.Any() ? _mapper.Map<IEnumerable<GetProduct>>(result) : [];
        }
    }
}
