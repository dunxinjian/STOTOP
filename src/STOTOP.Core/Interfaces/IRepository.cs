namespace STOTOP.Core.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id);
    Task<List<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(long id);
    IQueryable<T> Query();
}
