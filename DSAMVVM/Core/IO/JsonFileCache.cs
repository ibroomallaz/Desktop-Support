using DSAMVVM.Core.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace DSAMVVM.Core.IO
{
    public sealed class JsonFileCache<T>(string path)
    {
        private readonly string _path = path ?? throw new ArgumentNullException(nameof(path));
        private static readonly JsonSerializerSettings Json = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };

        public async Task<T?> ReadAsync()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    Log.Info("Cache", $"cache.read.miss path=\"{_path}\"");
                    return default;
                }

                var fi = new FileInfo(_path);
                Log.Info("Cache", $"cache.read.hit path=\"{_path}\" bytes={fi.Length}");
                var s = await File.ReadAllTextAsync(_path, Encoding.UTF8);
                return JsonConvert.DeserializeObject<T>(s, Json);
            }
            catch (Exception ex)
            {
                Log.Error("Cache", $"cache.read.error path=\"{_path}\"", ex);
                return default;
            }
        }

        public async Task WriteAsync(T model)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            var existed = File.Exists(_path);
            var tmp = _path + ".tmp";

            try
            {
                var json = JsonConvert.SerializeObject(model, Formatting.None, Json);
                await File.WriteAllTextAsync(tmp, json, Encoding.UTF8);
                File.Copy(tmp, _path, overwrite: true);
                File.Delete(tmp);

                var kind = existed ? "overwrite" : "create";
                Log.Info("Cache", $"cache.write.{kind} path=\"{_path}\" bytes={json.Length}");
            }
            catch (Exception ex)
            {
                Log.Error("Cache", $"cache.write.error path=\"{_path}\"", ex);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }
    }
}
