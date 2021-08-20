using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unreal.ReplayLib.Attributes;
using Unreal.ReplayLib.Extensions;

namespace Unreal.ReplayLib.NetFields
{
    public class CoreRedirects
    {
        public Dictionary<string, string> PartialRedirects { get; private set; } = new();
        private readonly ConcurrentDictionary<string, string> _redirects = new();

        public CoreRedirects(NetFieldExportGroupInfo netFieldExportGroupInfo)
        {
            var netFields =
                netFieldExportGroupInfo.Types.Where(x => x.GetCustomAttribute<NetFieldExportGroupAttribute>() != null);

            foreach (var type in netFields)
            {
                var exportGroupAttribute = type.GetCustomAttribute<NetFieldExportGroupAttribute>();
                var partialAttributes = type.GetCustomAttributes<PartialNetFieldExportGroup>();
                var redirectAttributes = type.GetCustomAttributes<RedirectPathAttribute>();

                foreach (var partialAttribute in partialAttributes)
                {
                    PartialRedirects.TryAdd(partialAttribute.PartialPath, exportGroupAttribute.Path);
                }

                if (redirectAttributes != null)
                {
                    foreach (var redirectAttribute in redirectAttributes)
                    {
                        _redirects.TryAdd(redirectAttribute.Path, exportGroupAttribute.Path.RemoveAllPathPrefixes());
                    }
                }
            }
        }

        public string GetRedirect(string path) => _redirects.TryGetValue(path, out var result)
            ? result
            : path;

        public bool ContainsRedirect(string path) => _redirects.ContainsKey(path);
    }
}