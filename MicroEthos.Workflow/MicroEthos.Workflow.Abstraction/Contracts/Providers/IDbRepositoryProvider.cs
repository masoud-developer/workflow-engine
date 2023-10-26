using System.Linq.Expressions;
using MongoDB.Bson;

namespace MicroEthos.Workflow.Abstraction.Contracts.Providers;

public interface IRepository<T> where T : class, new()
{
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate,
        params Expression<Func<T, object>>[] excludes);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null);
    IQueryable<T> List(Expression<Func<T, bool>> predicate = null);
    Task<T?> GetAsync(Expression<Func<T, bool>> predicate);
    Task<T> GetAsync(Expression<Func<T, bool>> predicate,
        params Expression<Func<T, object>>[] excludes);
    Task<T> GetByIdAsync(Guid id);
    Task<T> AddAsync(T entity);
    Task<bool> AddRangeAsync(IEnumerable<T> entities);
    Task<T> UpdateAsync(Guid id, T entity);
    Task<T> UpdateAsync(T entity, Expression<Func<T, bool>> predicate);
    Task<T> DeleteAsync(T entity);
    Task<T> DeleteAsync(Guid id);
    Task<T> DeleteAsync(Expression<Func<T, bool>> predicate);
    Task<long> CountAsync(Expression<Func<T, bool>> filter);
    Task<BsonDocument> RunCommand(string command);
    Task<List<BsonDocument>> GetIndexes(string command);
}