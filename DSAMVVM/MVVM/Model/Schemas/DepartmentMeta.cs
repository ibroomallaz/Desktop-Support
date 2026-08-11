using System.Runtime.Serialization;

namespace DSAMVVM.MVVM.Model.Schemas
{
    public sealed class DepartmentMeta : JsonMetaBase
    {
        public DepartmentMeta()
        {
            SchemaVersion = Globals.g_DepartmentJSONSchema;
        }

        // Automatically enforces the current Department schema version when reading old files
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (SchemaVersion < Globals.g_DepartmentJSONSchema)
            {
                SchemaVersion = Globals.g_DepartmentJSONSchema;
            }
        }
    }
}