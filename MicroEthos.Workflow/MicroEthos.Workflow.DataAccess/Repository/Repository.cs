using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MongoDB.Driver;

namespace MicroEthos.Workflow.DataAccess.Repository;

public class MongoDbRepository<T> : BaseRepository<Guid, T>, IRepository<T> where T : MongoDbEntity, new()
{
    public override Task<T> GetByIdAsync(Guid id)
    {
        return _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public override async Task<T> UpdateAsync(Guid id, T entity)
    {
        return await _collection.FindOneAndReplaceAsync(x => x.Id == id, entity);
    }

    public override async Task<T> DeleteAsync(T entity)
    {
        return await _collection.FindOneAndDeleteAsync(x => x.Id == entity.Id);
    }

    public override async Task<T> DeleteAsync(Guid id)
    {
        return await _collection.FindOneAndDeleteAsync(x => x.Id == id);
    }
}