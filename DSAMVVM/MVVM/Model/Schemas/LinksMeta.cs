using System.Runtime.Serialization;

namespace DSAMVVM.MVVM.Model.Schemas
{
    public sealed class LinksMeta : JsonMetaBase
    {
        public LinksMeta()
        {
            SchemaVersion = Globals.g_LinkJSONSchema;
        }

        // Automatically enforces the current Links schema version when reading old files
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (SchemaVersion < Globals.g_LinkJSONSchema)
            {
                SchemaVersion = Globals.g_LinkJSONSchema;
            }
        }
    }
}