using System.Runtime.Serialization;

namespace DSAMVVM.MVVM.Model.Schemas
{
    public sealed class ServiceMeowMeta : JsonMetaBase
    {
        public ServiceMeowMeta()
        {
            SchemaVersion = Globals.g_ServiceMeowJSONSchema;
        }

        // Automatically enforces the current ServiceMeow schema version when reading old files
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (SchemaVersion < Globals.g_ServiceMeowJSONSchema)
            {
                SchemaVersion = Globals.g_ServiceMeowJSONSchema;
            }
        }
    }
}
