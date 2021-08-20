using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.NetFields;

namespace Unreal.ReplayLib.Extensions
{
    public static class ExportGroupFieldPrinter
    {
        public static void PrintFileExcludingExisting(string title, string baseDir,
            ExportGroupFieldInfo exportGroupFieldInfo,
            NetFieldExportGroupInfo netFieldExportGroupInfo,
            bool includeGroups = true, bool includeRpcs = true)
        {
            var netFieldParser = new NetFieldParser(netFieldExportGroupInfo);
            var filteredFieldInfo = new ExportGroupFieldInfo
            {
                GroupToFieldDict = new ConcurrentDictionary<string, HashSet<string>>(exportGroupFieldInfo
                    .GroupToFieldDict
                    .Where(x => !netFieldParser.ContainsPath(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value))
            };

            foreach (var (group, fields) in filteredFieldInfo.GroupToFieldDict)
            {
                var fixedGroup = group.StartsWith("/") ? group[1..] : group;
                var fileDir = Path.GetDirectoryName(CleanName(fixedGroup));
                var fileName = CleanName(group.RemoveAllPathPrefixes()) + ".cs";
                var typeDir = group.EndsWith("_ClassNetCache") ? "ClassNetCaches" : "Groups";
                var filePath = Path.Combine(baseDir, typeDir, fileDir, fileName);
                Directory.CreateDirectory(new FileInfo(filePath).DirectoryName);
                var sb = new StringBuilder();
                PrintFile(sb, exportGroupFieldInfo, title, group, fields, includeGroups, includeRpcs);
                var tree = CSharpSyntaxTree.ParseText(sb.ToString());
                var root = tree.GetRoot().NormalizeWhitespace();
                var ret = root.ToFullString();
                File.WriteAllText(filePath, ret);
            }
        }

        public static void PrintFile(StringBuilder sb, ExportGroupFieldInfo exportGroupFieldInfo, string title,
            string group,
            HashSet<string> fields, bool includeGroups = true,
            bool includeRpcs = true)
        {
            sb.AppendLine("using Unreal.ReplayLib.Attributes;");
            sb.AppendLine("using Unreal.ReplayLib.Contracts;");
            sb.AppendLine("using Unreal.ReplayLib.Models;");
            sb.AppendLine("using Unreal.ReplayLib.Models.Enums;");
            sb.AppendLine("");
            sb.AppendLine($"namespace ${title}ReplayLib.NetFieldExports.Generated");
            sb.AppendLine("{");
            PrintClass(sb, exportGroupFieldInfo, includeGroups, includeRpcs, group, fields);
            sb.AppendLine("}");
        }

        private static void PrintClass(StringBuilder sb, ExportGroupFieldInfo exportGroupFieldInfo, bool includeGroups,
            bool includeRpcs, string group, HashSet<string> fields)
        {
            if (!group.EndsWith("_ClassNetCache"))
            {
                if (includeGroups)
                {
                    var cleanedName = group.RemoveAllPathPrefixes();
                    var redirects = exportGroupFieldInfo.GroupToFieldDict
                        .Where(x => x.Key.RemoveAllPathPrefixes() == cleanedName)
                        .Where(x => x.Key != group)
                        .Select(x => x.Key)
                        .ToHashSet();
                    if (redirects.Count > 0 && !group.Contains("/"))
                    {
                        return;
                    }

                    PrintExportGroup(sb, group, fields, redirects);
                }
            }
            else
            {
                if (includeRpcs)
                {
                    PrintClassCacheRpc(sb, group, fields);
                }
            }
        }

        public static void PrintExportGroup(StringBuilder sb, string group, HashSet<string> fields,
            HashSet<string> redirects)
        {
            var cleanedName = StripAndCleanName(group);
            sb.AppendLine($"[NetFieldExportGroup(\"{group}\")]");

            foreach (var redirect in redirects)
            {
                sb.AppendLine($"[RedirectPath(\"{redirect}\")]");
            }

            sb.AppendLine($"public class {cleanedName} : NetFieldExportGroupBase");
            sb.AppendLine("{");
            foreach (var field in fields.Where(x => x != null))
            {
                var fieldType = GuessFieldType(cleanedName);
                PrintExportField(sb, field, fieldType);
            }

            sb.AppendLine("}");
        }

        private static string GuessFieldType(string cleanedName)
        {
            string fieldType = null;

            if (Regex.IsMatch(cleanedName, "b[A-Z][a-zA-Z]+?"))
            {
                fieldType = "boolean";
            }

            return fieldType;
        }

        public static void PrintExportField(StringBuilder sb, string field, string fieldType)
        {
            var cleanedName = CleanName(field);

            switch (fieldType)
            {
                case "boolean":
                    sb.AppendLine($"[NetFieldExport(\"{field}\", RepLayoutCmdType.PropertyBool)]");
                    sb.AppendLine($"public bool? {cleanedName} {{ get; set; }}");
                    break;
                default:
                    sb.AppendLine($"[NetFieldExport(\"{field}\", RepLayoutCmdType.Debug)]");
                    sb.AppendLine($"public DebuggingObject {cleanedName} {{ get; set; }}");
                    break;
            }
        }

        public static void PrintClassCacheRpc(StringBuilder sb, string rpc, HashSet<string> properties)
        {
            var cleanedName = StripAndCleanName(rpc);
            sb.AppendLine($"[NetFieldExportRpc(\"{rpc}\")]");
            sb.AppendLine($"public class {cleanedName}");
            sb.AppendLine("{");
            foreach (var property in properties.Where(x => x != null))
            {
                PrintRpcProperty(sb, property);
            }

            sb.AppendLine("}");
        }

        public static void PrintRpcProperty(StringBuilder sb, string property)
        {
            var cleanedName = CleanName(property);
            sb.AppendLine($"[NetFieldExportRpcProperty(\"{property}\", \"<TODO_FIND_PATH>\")]");
            sb.AppendLine($"public object {cleanedName} {{ get; set; }}");
        }

        private static string CleanName(string name) =>
            name
                .Replace(' ', '_')
                .Replace('.', '_')
                .Replace(':', '_')
                .Replace("(", "")
                .Replace(")", "");

        private static string StripAndCleanName(string name) =>
            CleanName(name.RemoveAllPathPrefixes());
    }
}