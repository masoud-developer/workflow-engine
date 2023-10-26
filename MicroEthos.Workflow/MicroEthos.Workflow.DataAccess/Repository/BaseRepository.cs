using System.Linq.Expressions;
using MicroEthos.Common.Utils.Helpers;
using MicroEthos.Workflow.Abstraction.Models.Database.Attributes;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MicroEthos.Workflow.DataAccess.Repository;

public abstract class BaseRepository<TKey, T> where T : class, new()
{
    protected readonly IMongoCollection<T> _collection;
    protected readonly IMongoDatabase _db;

    public BaseRepository()
    {
        var client = new MongoClient(EnvironmentHelper.Get("MONGO_DB_URL")!);
        var db = client.GetDatabase(EnvironmentHelper.Get("MONGO_DB_NAME")!);
        _collection = db.GetCollection<T>(GetCollectionName());
        _db = db;
    }
    
    public async Task<List<BsonDocument>> GetIndexes(string command)
    {
        return _collection.Indexes.List().ToList();
    }

    public async Task<BsonDocument> RunCommand(string command)
    {
        var result = await _db.RunCommandAsync<BsonDocument>(command);
        return result;
    }

    public virtual Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return _collection.Find(predicate ?? (t => true)).ToListAsync();
    }

    public virtual Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate,
        params Expression<Func<T, object>>[] excludes)
    {
        var projection = Builders<T>.Projection;
        ProjectionDefinition<T> projectionDefinition = null;
        foreach (var exclude in excludes)
        {
            projectionDefinition = projection.Exclude(exclude);
        }

        return _collection.Find(predicate ?? (t => true)).Project<T>(projectionDefinition).ToListAsync();
    }

    public virtual IQueryable<T> List(Expression<Func<T, bool>> predicate = null)
    {
        return predicate == null
            ? _collection.AsQueryable()
            : _collection.AsQueryable().Where(predicate);
    }

    public virtual Task<T> GetAsync(Expression<Func<T, bool>> predicate)
    {
        return _collection.Find(predicate).FirstOrDefaultAsync();
    }
    
    public virtual Task<T> GetAsync(Expression<Func<T, bool>> predicate,
        params Expression<Func<T, object>>[] excludes)
    {
        var projection = Builders<T>.Projection;
        ProjectionDefinition<T> projectionDefinition = null;
        foreach (var exclude in excludes)
        {
            projectionDefinition = projection.Exclude(exclude);
        }

        return _collection.Find(predicate ?? (t => true)).Project<T>(projectionDefinition).FirstOrDefaultAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        var options = new InsertOneOptions { BypassDocumentValidation = false };
        await _collection.InsertOneAsync(entity, options);
        return entity;
    }

    public virtual async Task<bool> AddRangeAsync(IEnumerable<T> entities)
    {
        var options = new BulkWriteOptions { IsOrdered = false, BypassDocumentValidation = false };
        return (await _collection.BulkWriteAsync((IEnumerable<WriteModel<T>>)entities, options)).IsAcknowledged;
    }

    public virtual async Task<T> UpdateAsync(T entity, Expression<Func<T, bool>> predicate)
    {
        return await _collection.FindOneAndReplaceAsync(predicate, entity);
    }

    public virtual async Task<T> DeleteAsync(Expression<Func<T, bool>> filter)
    {
        return await _collection.FindOneAndDeleteAsync(filter);
    }

    public virtual async Task<long> CountAsync(Expression<Func<T, bool>> filter)
    {
        return await _collection.CountDocumentsAsync(filter);
    }

    private static string GetCollectionName()
    {
        return (typeof(T).GetCustomAttributes(typeof(BsonCollectionAttribute), true).FirstOrDefault()
            as BsonCollectionAttribute)?.CollectionName ?? typeof(T).Name.ToLowerInvariant();
    }

    public abstract Task<T> DeleteAsync(TKey id);
    public abstract Task<T> DeleteAsync(T entity);
    public abstract Task<T> UpdateAsync(TKey id, T entity);
    public abstract Task<T> GetByIdAsync(TKey id);
}