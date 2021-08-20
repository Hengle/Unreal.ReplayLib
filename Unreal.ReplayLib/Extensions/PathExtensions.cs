using System.Linq;

namespace Unreal.ReplayLib.Extensions
{
    public static class PathExtensions
    {
        public static string RemoveAllPathPrefixes(this string path)
        {
            path = RemovePathPrefix(path, "Default__");

            for (var i = path.Length - 1; i >= 0; i--)
            {
                switch (path[i])
                {
                    case '.':
                        return path[(i + 1)..];
                    case '/':
                        return path;
                }
            }

            return path;
        }

        private static string CleanPathSuffix(string path)
        {
            for (var i = path.Length - 1; i >= 0; i--)
            {
                var isDigit = (path[i] ^ '0') <= 9;
                var isUnderscore = path[i] == '_';

                if (!isDigit && !isUnderscore)
                {
                    return path[..(i + 1)];
                }
            }

            return path;
        }

        private static string RemovePathPrefix(string path, string toRemove)
        {
            if (toRemove.Length > path.Length)
            {
                return path;
            }

            return toRemove
                .Where((t, i) => path[i] != t)
                .Any()
                ? path
                : path[toRemove.Length..];
        }
    }
}