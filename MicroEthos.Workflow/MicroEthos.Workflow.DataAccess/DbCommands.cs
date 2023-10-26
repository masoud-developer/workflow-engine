namespace MicroEthos.Workflow.DataAccess;

public class DbCommands
{
    public const string CreateModulesCollectionUniqueIndex = "{ createIndexes: 'modules', indexes: [ { key: { AssemblyName: 1 }, name: 'assembly-name', unique: true } ] }";
}