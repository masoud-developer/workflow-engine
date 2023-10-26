using System.Linq.Expressions;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MongoDB.Driver;

namespace MicroEthos.Workflow.DataAccess.Repository;

public class GeneralRepository<T> : BaseRepository<string, T> where T : MongoDbGeneralEntity, new()
{
    public override Task<T> DeleteAsync(string id)
    {
        return Task.FromResult(default(T));
    }

    public override Task<T> DeleteAsync(T entity)
    {
        return Task.FromResult(default(T));
    }

    public override Task<T> UpdateAsync(string id, T entity)
    {
        throw new NotImplementedException();
    }
    
    public virtual async Task<long> UpdateManyAsync(Expression<Func<T, bool>> predicate, params (Expression<Func<T, object>> Field, object Value)[] updates)
    {
        if (updates.Length < 1)
            return 0;
        var pred = Builders<T>.Filter.Where(predicate);
        var updateDef = Builders<T>.Update.Set(updates[0].Field, updates[0].Value);
        foreach (var item in updates.Select((s, i) => new { update = s, index = i }))
        {
            if(item.index == 0)
                continue;
            updateDef = updateDef.Set(item.update.Field, item.update.Value);
        }
        var res = await _collection.UpdateManyAsync(pred, updateDef);
        return res.ModifiedCount;
    }
    
    public virtual async Task<long> DeleteManyAsync(Expression<Func<T, bool>> predicate)
    {
        // if (updates.Length < 1)
        //     return 0;
        // foreach (var item in updates.Select((s, i) => new { update = s, index = i }))
        // {
        //     if(item.index == 0)
        //         continue;
        //     updateDef = updateDef.Set(item.update.Field, item.update.Value);
        // }
        var res = await _collection.DeleteManyAsync(predicate);
        return res.DeletedCount;
    }

    public override async Task<T> GetByIdAsync(string id)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }
}