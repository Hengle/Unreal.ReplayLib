using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;

namespace Unreal.ReplayLib.Models
{
    public class ExportGroupFieldInfo
    {
        public ConcurrentDictionary<string, HashSet<string>> GroupToFieldDict { get; init; } = new();

        public void AddExportGroupDict(Dictionary<string, HashSet<string>> replayExportGroupDict)
        {
            foreach (var (key, value) in replayExportGroupDict)
            {
                GroupToFieldDict.TryAdd(key, new HashSet<string>());
                if (GroupToFieldDict.TryGetValue(key, out var fieldSet))
                {
                    foreach (var field in value)
                    {
                        fieldSet.Add(field);
                    }
                }
            }
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions()
            {
                WriteIndented = true
            });
        }
    }
}