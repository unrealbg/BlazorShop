namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

    public class CategoryService : ICategoryService
    {
        private readonly IGenericRepository<Category> _categoryRepository;
        private readonly IMapper _mapper;

        public CategoryService(IGenericRepository<Category> categoryRepository, IMapper mapper)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<GetCategory>> GetAllAsync()
        {
            var result = await _categoryRepository.GetAllAsync();
            return result.Any() ? _mapper.Map<IEnumerable<GetCategory>>(result) : new List<GetCategory>();
        }

        public async Task<GetCategory> GetByIdAsync(Guid id)
        {
            var result = await _categoryRepository.GetByIdAsync(id);
            return result != null ? _mapper.Map<GetCategory>(result) : new GetCategory();
        }

        public async Task<ServiceResponse> AddAsync(CreateCategory category)
        {
            int result = await _categoryRepository.AddAsync(_mapper.Map<Category>(category));

            return result > 0 ? new ServiceResponse(true, "Category added successfully") : new ServiceResponse(false, "Category not added");
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateCategory category)
        {
            int result = await _categoryRepository.UpdateAsync(_mapper.Map<Category>(category));
            return result > 0 ? new ServiceResponse(true, "Category updated successfully") : new ServiceResponse(false, "Category not found");
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var result = await _categoryRepository.DeleteAsync(id);

            return result > 0 ? new ServiceResponse(true, "Category deleted successfully") : new ServiceResponse(false, "Category not found");
        }
    }
}
