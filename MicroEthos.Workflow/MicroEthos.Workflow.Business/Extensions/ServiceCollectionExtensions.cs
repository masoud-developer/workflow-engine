using MicroEthos.Common.Utils.Helpers;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Persistence.MongoDB.Services;
using MongoPersistenceProvider = MicroEthos.Workflow.Business.Workflow.Providers.MongoPersistenceProvider;

namespace MicroEthos.Workflow.Business.Extensions;

public static class ServiceCollectionExtensions
{
    public static WorkflowOptions UseMongoDBPersistance(
        this WorkflowOptions options,
        string mongoUrl,
        string databaseName,
        Action<MongoClientSettings> configureClient = default)
    {
        options.UsePersistence(sp =>
        {
            var mongoClientSettings = MongoClientSettings.FromConnectionString(EnvironmentHelper.Get("MONGO_DB_URL"));
            configureClient?.Invoke(mongoClientSettings);
            var client = new MongoClient(mongoClientSettings);
            var db = client.GetDatabase(EnvironmentHelper.Get("MONGO_DB_NAME"));
            return new MongoPersistenceProvider(db);
        });
        options.Services.AddTransient<IWorkflowPurger>(sp =>
        {
            var mongoClientSettings = MongoClientSettings.FromConnectionString(EnvironmentHelper.Get("MONGO_DB_URL"));
            configureClient?.Invoke(mongoClientSettings);
            var client = new MongoClient(mongoClientSettings);
            var db = client.GetDatabase(EnvironmentHelper.Get("MONGO_DB_NAME"));
            return new WorkflowPurger(db);
        });
        return options;
    }
}