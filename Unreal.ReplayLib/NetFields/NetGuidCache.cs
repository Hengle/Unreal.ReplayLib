using System;
using System.Collections.Generic;
using Unreal.ReplayLib.Extensions;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.NetFields
{
    public class NetGuidCache
    {
        private readonly Dictionary<uint, string> _netGuidToPathDict = new();
        private readonly Dictionary<uint, NetFieldExportGroup> _archTypeToExportGroupDict = new();
        private readonly Dictionary<uint, NetFieldExportGroup> _pathIndexToExportGroupDict = new();
        private readonly Dictionary<string, NetFieldExportGroup> _pathToExportGroupDict = new();
        private readonly Dictionary<string, string> _partialPathNameToPathDict = new();
        private readonly Dictionary<string, string> _cleanedClassNetCacheToPathDict = new();
        private readonly NetFieldParser _parser;
        private readonly HashSet<string> _unknownGroups = new();

        public Dictionary<string, NetFieldExportGroup> PathToExportGroupDict => _pathToExportGroupDict;

        public Dictionary<uint, string> NetGuidToPathName => _netGuidToPathDict;
        public NetFieldExportGroup NetworkGameplayTagNodeIndex { get; private set; }

        public NetGuidCache(NetFieldParser parser)
        {
            _parser = parser;
        }

        public void Clear()
        {
            _netGuidToPathDict.Clear();
            _archTypeToExportGroupDict.Clear();
            _pathIndexToExportGroupDict.Clear();
            _pathIndexToExportGroupDict.Clear();
            _pathToExportGroupDict.Clear();
            _partialPathNameToPathDict.Clear();
            _cleanedClassNetCacheToPathDict.Clear();
            NetworkGameplayTagNodeIndex = null;
        }

        public void AddPathByGuid(uint netGuidValue, string path)
        {
            var cleanedPath = path.RemoveAllPathPrefixes();
            _netGuidToPathDict[netGuidValue] = cleanedPath;
        }

        
        public void AddGroupByPath(string path, NetFieldExportGroup exportGroup)
        {
            if (NetworkGameplayTagNodeIndex == null && path == "NetworkGameplayTagNodeIndex")
            {
                NetworkGameplayTagNodeIndex = exportGroup;
            }

            //Easiest way to do this update
            if (path.EndsWith("ClassNetCache"))
            {
                exportGroup.PathName = exportGroup.PathName.RemoveAllPathPrefixes();
            }

            exportGroup.CleanedPath = path.RemoveAllPathPrefixes();

            _pathToExportGroupDict[exportGroup.CleanedPath] = exportGroup;

            _parser.UpdateExportGroup(exportGroup);

            //Check if partial path
            foreach (var partialRedirectKvp in _parser.CoreRedirects.PartialRedirects)
            {
                if (path.StartsWith(partialRedirectKvp.Key, StringComparison.Ordinal))
                {
                    _partialPathNameToPathDict.TryAdd(path, partialRedirectKvp.Value.RemoveAllPathPrefixes());
                    _partialPathNameToPathDict.TryAdd(path.RemoveAllPathPrefixes(),
                        partialRedirectKvp.Value.RemoveAllPathPrefixes());

                    break;
                }
            }
        }
        
        public void AddToExportGroupMap(string group, NetFieldExportGroup exportGroup)
        {
            if (NetworkGameplayTagNodeIndex == null && group == "NetworkGameplayTagNodeIndex")
            {
                NetworkGameplayTagNodeIndex = exportGroup;
            }

            //Easiest way to do this update
            if(group.EndsWith("ClassNetCache"))
            {
                exportGroup.PathName = exportGroup.PathName.RemoveAllPathPrefixes();
            }

            exportGroup.CleanedPath = group.RemoveAllPathPrefixes();

            _pathToExportGroupDict[exportGroup.CleanedPath] = exportGroup;

            _parser.UpdateExportGroup(exportGroup);

            //Check if partial path
            foreach (var partialRedirectKvp in _parser.CoreRedirects.PartialRedirects)
            {
                if (group.StartsWith(partialRedirectKvp.Key, StringComparison.Ordinal))
                {
                    _partialPathNameToPathDict.TryAdd(group, partialRedirectKvp.Value.RemoveAllPathPrefixes());
                    _partialPathNameToPathDict.TryAdd(group.RemoveAllPathPrefixes(), partialRedirectKvp.Value.RemoveAllPathPrefixes());
                    break;
                }
            }
        }
        
        public NetFieldExportGroup GetGroupByPathIndex(uint pathIndex) =>
            _pathIndexToExportGroupDict.GetValueOrDefault(pathIndex, null);

        public void AddGroupByPathIndex(uint pathIndex, NetFieldExportGroup group) =>
            _pathIndexToExportGroupDict[pathIndex] = group;

        public bool TryGetNetGuidToPath(uint netGuid, out string path) =>
            _netGuidToPathDict.TryGetValue(netGuid, out path);

        public NetFieldExportGroup GetNetFieldExportGroup(string pathName)
        {
            if (string.IsNullOrEmpty(pathName))
            {
                return null;
            }

            return _pathToExportGroupDict.TryGetValue(pathName, out var netFieldExportGroup)
                ? netFieldExportGroup
                : null;
        }

        public NetFieldExportGroup GetNetFieldExportGroup(uint guid)
        {
            if (_archTypeToExportGroupDict.TryGetValue(guid, out var group))
            {
                return group;
            }

            if (!_netGuidToPathDict.ContainsKey(guid))
            {
                return null;
            }

            var path = _netGuidToPathDict[guid];
            path = _parser.CoreRedirects.GetRedirect(path);

            if (_partialPathNameToPathDict.TryGetValue(path, out var redirectPath))
            {
                path = redirectPath;
            }

            if (_pathToExportGroupDict.TryGetValue(path, out var exportGroup))
            {
                _archTypeToExportGroupDict[guid] = exportGroup;
                return exportGroup;
            }

            _unknownGroups.Add(path);

            return null;
        }

        public NetFieldExportGroup GetNetFieldExportGroupForClassNetCache(string cleanedClassNetCache)
        {
            if (!_cleanedClassNetCacheToPathDict.TryGetValue(cleanedClassNetCache, out var classNetCachePath))
            {
                classNetCachePath = $"{cleanedClassNetCache.RemoveAllPathPrefixes()}_ClassNetCache";
                _cleanedClassNetCacheToPathDict[cleanedClassNetCache] = classNetCachePath;
            }

            return !_pathToExportGroupDict.TryGetValue(classNetCachePath, out var exportGroup)
                ? default
                : exportGroup;
        }
    }
}