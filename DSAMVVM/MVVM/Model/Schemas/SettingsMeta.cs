using System.Runtime.Serialization;

namespace DSAMVVM.MVVM.Model.Schemas
{
    public sealed class SettingsMeta : JsonMetaBase
    {
        public SettingsMeta()
        {
            // This sets the version for brand new, factory-default files.
            SchemaVersion = Globals.g_SettingsSchema;
        }


        //Schema Upgrade Enforcer
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            // If the JSON file we just read has an older schema version, 
            // force it to upgrade to the current application standard in memory.
            if (SchemaVersion < Globals.g_SettingsSchema)
            {
                SchemaVersion = Globals.g_SettingsSchema;
            }
        }
    }
}