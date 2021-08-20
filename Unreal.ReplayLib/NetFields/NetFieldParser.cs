using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unreal.ReplayLib.Attributes;
using Unreal.ReplayLib.Extensions;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.NetFields
{
    public sealed class NetFieldParser
    {
        private readonly NetFieldParserInfo _parserInfo;
        private readonly SingleInstanceExport[] _objects;

        public CoreRedirects CoreRedirects => _parserInfo.CoreRedirects;

        internal NetFieldParser(NetFieldExportGroupInfo netFieldExportGroupInfo)
        {
            var allTypes = netFieldExportGroupInfo.Types;
            var netFields = allTypes
                .Where(x => x.GetCustomAttribute<NetFieldExportGroupAttribute>(false) != null)
                .ToList();
            var classNetCaches = allTypes
                .Where(x => x.GetCustomAttribute<NetFieldExportRpcAttribute>(false) != null)
                .ToList();
            var propertyTypes = allTypes
                .Where(x => typeof(IProperty).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .ToList();

            _parserInfo = new NetFieldParserInfo(netFieldExportGroupInfo);

            LoadNetFields(netFields);
            LoadClassNetCaches(classNetCaches);
            LoadPropertyTypes(propertyTypes);

            _objects = new SingleInstanceExport[_parserInfo.LinqCache.TotalTypes];
            for (var i = 0; i < _parserInfo.NetFieldGroups.Length; i++)
            {
                var group = _parserInfo.NetFieldGroups[i];

                _objects[group.TypeId] = new SingleInstanceExport
                {
                    Instance = (NetFieldExportGroupBase)Activator.CreateInstance(group.Type),
                    ChangedProperties = new FastClearArray<NetFieldInfo>(group.Properties.Length)
                };
            }
        }

        private static Action<NetFieldExportGroupBase, object> CreateSetter(PropertyInfo propertyInfo)
        {
            var field = GetBackingField(propertyInfo);

            var methodName = field.ReflectedType.FullName + ".set_" + field.Name;
            var setterMethod = new DynamicMethod(methodName, null,
                new[] { typeof(NetFieldExportGroupBase), typeof(object) }, true);
            var gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Unbox_Any, field.FieldType);
                gen.Emit(OpCodes.Stfld, field);
            }

            gen.Emit(OpCodes.Ret);

            return (Action<NetFieldExportGroupBase, object>)setterMethod.CreateDelegate(
                typeof(Action<NetFieldExportGroupBase, object>));

            static FieldInfo GetBackingField(PropertyInfo property) =>
                property.DeclaringType.GetField($"<{property.Name}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
        }

        internal void UpdateExportGroup(NetFieldExportGroup group)
        {
            if (_parserInfo.NetFieldGroups.TryGetIndex(group.PathName, out var val))
            {
                group.GroupId = val;
            }
        }

        private void LoadNetFields(List<Type> netFields)
        {
            var typeIds = new Dictionary<Type, int>();
            var typeId = 0;

            foreach (var type in netFields)
            {
                typeIds.TryAdd(type, typeId++);

                var attribute = type.GetCustomAttribute<NetFieldExportGroupAttribute>();

                if (attribute?.PlayerController != null)
                {
                    _parserInfo.PlayerControllers.Add(attribute.PlayerController);
                }

                var info = new NetFieldGroupInfo
                {
                    Type = type,
                    Attribute = attribute,
                    UsesHandles = typeof(HandleNetFieldExportGroup).IsAssignableFrom(type),
                    SingleInstance = true
                };

                // typeof(ISingleInstance).IsAssignableFrom(type);

                _parserInfo.NetFieldGroups.Add(attribute.Path, info);

                info.TypeId = _parserInfo.LinqCache.AddExportType(info.Type);

                foreach (var property in type.GetProperties())
                {
                    var netFieldExportAttribute =
                        property.GetCustomAttribute<NetFieldExportAttribute>();

                    if (netFieldExportAttribute == null)
                    {
                        continue;
                    }

                    var fieldInfo = new NetFieldInfo
                    {
                        Attribute = netFieldExportAttribute,
                        PropertyInfo = property
                    };

                    if (property.PropertyType.IsEnum)
                    {
                        fieldInfo.DefaultValue = Enum.GetValues(property.PropertyType).Cast<int>().Max();
                    }
                    else if (property.PropertyType.IsValueType)
                    {
                        fieldInfo.DefaultValue = Activator.CreateInstance(property.PropertyType);
                    }

                    info.Properties.Add(netFieldExportAttribute.Name, fieldInfo);

                    //No reason to add ignored types
                    if (netFieldExportAttribute.Type != RepLayoutCmdType.Ignore)
                    {
                        if (property.PropertyType.IsArray)
                        {
                            var elementType = property.PropertyType.GetElementType();

                            if (typeof(NetFieldExportGroupBase).IsAssignableFrom(elementType))
                            {
                                fieldInfo.ElementTypeId = _parserInfo.LinqCache.AddExportType(elementType);
                            }
                        }
                    }
                }
            }
        }

        private void LoadClassNetCaches(List<Type> classNetCaches)
        {
            foreach (var type in classNetCaches)
            {
                var attribute = type.GetCustomAttribute<NetFieldExportRpcAttribute>();
                var info = new NetRpcFieldGroupInfo();

                _parserInfo.NetRpcStructureTypes[attribute.PathName] = info;

                foreach (var property in type.GetProperties())
                {
                    var propertyAttribute = property.GetCustomAttribute<NetFieldExportRpcPropertyAttribute>();

                    if (propertyAttribute != null)
                    {
                        info.PathNames.TryAdd(propertyAttribute.Name, new NetRpcFieldInfo
                        {
                            PropertyInfo = property,
                            Attribute = propertyAttribute,
                            IsCustomStructure = propertyAttribute.CustomStructure
                        });
                    }
                }
            }
        }

        private void LoadPropertyTypes(List<Type> propertyTypes)
        {
            //Type layout for dynamic arrays
            _parserInfo.PrimitiveTypeLayout.Add(typeof(bool), RepLayoutCmdType.PropertyBool);
            _parserInfo.PrimitiveTypeLayout.Add(typeof(byte), RepLayoutCmdType.PropertyByte);
            _parserInfo.PrimitiveTypeLayout.Add(typeof(ushort), RepLayoutCmdType.PropertyUInt16);
            _parserInfo.PrimitiveTypeLayout.Add(typeof(int), RepLayoutCmdType.PropertyInt);
            _parserInfo.PrimitiveTypeLayout.Add(typeof(uint), RepLayoutCmdType.PropertyUInt32);
            _parserInfo.PrimitiveTypeLayout.Add(typeof(ulong), RepLayoutCmdType.PropertyUInt64);
            _parserInfo.PrimitiveTypeLayout.Add(typeof(float), RepLayoutCmdType.PropertyFloat);
            _parserInfo.PrimitiveTypeLayout.Add(typeof(string), RepLayoutCmdType.PropertyString);
            _parserInfo.PrimitiveTypeLayout.Add(typeof(object), RepLayoutCmdType.Ignore);

            //Allows deserializing IProperty type arrays
            foreach (var iPropertyType in propertyTypes)
            {
                _parserInfo.PrimitiveTypeLayout.Add(iPropertyType, RepLayoutCmdType.Property);
            }
        }

        internal bool IsPlayerController(string name) => _parserInfo.PlayerControllers.Contains(name);

        internal string GetClassNetPropertyPathname(string netCache, string property, out bool readChecksumBit)
        {
            readChecksumBit = false;

            if (_parserInfo.NetRpcStructureTypes.TryGetValue(netCache, out var netCacheFieldGroupInfo))
            {
                if (netCacheFieldGroupInfo.PathNames.TryGetValue(property, out var rpcAttribute))
                {
                    readChecksumBit = rpcAttribute.Attribute.ReadChecksumBit;
                    return rpcAttribute.Attribute.TypePathName;
                }
                else
                {
                    //Debugging
                }
            }
            else
            {
                //Debugging
            }

            return null;
        }

        internal bool TryGetNetFieldGroupRpc(string classNetPathName, string property, out NetRpcFieldInfo netFieldInfo,
            out bool willParse)
        {
            willParse = false;
            netFieldInfo = null;

            if (_parserInfo.NetRpcStructureTypes.TryGetValue(classNetPathName, out var groups))
            {
                willParse = true;
                if (groups.PathNames.TryGetValue(property, out var netFieldExportRpcPropertyAttribute))
                {
                    netFieldInfo = netFieldExportRpcPropertyAttribute;

                    return true;
                }
            }

            return false;
        }

        internal bool WillReadType(int groupId, out bool ignoreChannel)
        {
            ignoreChannel = false;

            if (groupId == -1)
            {
                return false;
            }

            var groupInfo = _parserInfo.NetFieldGroups[groupId];

            return true;
        }

        internal void ReadField(NetFieldExportGroupBase obj, NetFieldExport export, NetFieldExportGroup exportGroup,
            uint handle, NetBitReader netBitReader, bool singleInstance = true)
        {
            if (export.PropertyId == -2)
            {
                return;
            }

            var fixedExportName = export.Name;
            var isDebug = obj is DebuggingExportGroup;
            var groupId = exportGroup.GroupId;

            if (isDebug)
            {
                _parserInfo.NetFieldGroups.TryGetIndex("DebuggingExportGroup", out groupId);
                fixedExportName = "Handles";
            }

            if (exportGroup.GroupId == -1)
            {
                return;
            }

            var netGroupInfo = _parserInfo.NetFieldGroups[groupId];
            NetFieldInfo netFieldInfo = null;

            //Update
            if (!netGroupInfo.UsesHandles)
            {
                var propertyIndex = export.PropertyId;

                if (export.PropertyId == -1)
                {
                    if (!netGroupInfo.Properties.TryGetIndex(fixedExportName, out propertyIndex))
                    {
                        export.PropertyId = -2;
                        return;
                    }

                    export.PropertyId = propertyIndex;
                }

                netFieldInfo = netGroupInfo.Properties[propertyIndex];
            }

            if (netGroupInfo.UsesHandles && !netGroupInfo.HandleProperties.TryGetValue(handle, out netFieldInfo))
            {
                //Clean this up
                if (obj is HandleNetFieldExportGroup handleGroup)
                {
                    var data = ReadDataType(handleGroup.Type, netBitReader);
                    handleGroup.UnknownHandles.Add(handle, data);
                }

                return;
            }

            SetType(obj, netFieldInfo, netGroupInfo, exportGroup, handle, netBitReader, singleInstance);
        }

        private void SetType(NetFieldExportGroupBase obj, NetFieldInfo netFieldInfo, NetFieldGroupInfo groupInfo,
            NetFieldExportGroup exportGroup,
            uint handle, NetBitReader netBitReader, bool singleInstance)
        {
            var data = netFieldInfo.Attribute.Type switch
            {
                RepLayoutCmdType.DynamicArray =>
                    ReadArrayField(exportGroup, netFieldInfo, groupInfo, netBitReader),
                _ =>
                    ReadDataType(netFieldInfo.Attribute.Type, netBitReader, netFieldInfo.PropertyInfo.PropertyType)
            };

            if (data != null)
            {
                if (singleInstance)
                {
                    _objects[groupInfo.TypeId].ChangedProperties.Add(netFieldInfo);
                }

                netFieldInfo.SetMethod ??= CreateSetter(netFieldInfo.PropertyInfo);
                netFieldInfo.SetMethod(obj, data);
            }
        }

        private object ReadDataType(RepLayoutCmdType replayout, NetBitReader netBitReader, Type type = null)
        {
            object data = null;

            switch (replayout)
            {
                case RepLayoutCmdType.Property:
                    data = _parserInfo.LinqCache.CreatePropertyObject(type);
                    ((IProperty)data).Serialize(netBitReader);
                    break;
                case RepLayoutCmdType.RepMovement:
                    data = netBitReader.SerializeRepMovement();
                    break;
                case RepLayoutCmdType.RepMovementWholeNumber:
                    data = netBitReader.SerializeRepMovement(VectorQuantization.RoundWholeNumber,
                        RotatorQuantization.ByteComponents, VectorQuantization.RoundWholeNumber);
                    break;
                case RepLayoutCmdType.PropertyBool:
                    data = netBitReader.SerializePropertyBool();
                    break;
                case RepLayoutCmdType.PropertyName:
                    data = netBitReader.SerializePropertyName();
                    break;
                case RepLayoutCmdType.PropertyFloat:
                    data = netBitReader.SerializePropertyFloat();
                    break;
                case RepLayoutCmdType.PropertyNativeBool:
                    data = netBitReader.SerializePropertyNativeBool();
                    break;
                case RepLayoutCmdType.PropertyNetId:
                    data = netBitReader.SerializePropertyNetId();
                    break;
                case RepLayoutCmdType.PropertyObject:
                    data = netBitReader.SerializePropertyObject();
                    break;
                case RepLayoutCmdType.PropertyPlane:
                    throw new NotImplementedException("Plane RepLayoutCmdType not implemented");
                case RepLayoutCmdType.PropertyRotator:
                    data = netBitReader.SerializePropertyRotator();
                    break;
                case RepLayoutCmdType.PropertyString:
                    data = netBitReader.SerializePropertyString();
                    break;
                case RepLayoutCmdType.PropertyVector10:
                    data = netBitReader.SerializePropertyVector10();
                    break;
                case RepLayoutCmdType.PropertyVector100:
                    data = netBitReader.SerializePropertyVector100();
                    break;
                case RepLayoutCmdType.PropertyVectorNormal:
                    data = netBitReader.SerializePropertyVectorNormal();
                    break;
                case RepLayoutCmdType.PropertyVectorQ:
                    data = netBitReader.SerializePropertyQuantizeVector();
                    break;
                case RepLayoutCmdType.Enum:
                    data = netBitReader.SerializeEnum();
                    break;
                case RepLayoutCmdType.PropertyByte:
                    data = (byte)netBitReader.ReadBitsToInt(netBitReader.GetBitsLeft());
                    break;
                case RepLayoutCmdType.PropertyInt:
                    data = netBitReader.ReadInt32();
                    break;
                case RepLayoutCmdType.PropertyUInt64:
                    data = netBitReader.ReadUInt64();
                    break;
                case RepLayoutCmdType.PropertyInt16:
                    data = netBitReader.ReadInt16();
                    break;
                case RepLayoutCmdType.PropertyUInt16:
                    data = netBitReader.ReadUInt16();
                    break;
                case RepLayoutCmdType.PropertyUInt32:
                    data = netBitReader.ReadUInt32();
                    break;
                case RepLayoutCmdType.PropertyVector:
                    data = netBitReader.SerializePropertyVector();
                    break;
                case RepLayoutCmdType.Ignore:
                    netBitReader.Seek(netBitReader.GetBitsLeft(), SeekOrigin.Current);
                    break;
                case RepLayoutCmdType.Debug:
                    break;
            }

            return data;
        }

        private Array ReadArrayField(NetFieldExportGroup netfieldExportGroup, NetFieldInfo fieldInfo,
            NetFieldGroupInfo groupInfo, NetBitReader netBitReader)
        {
            var arrayIndexes = netBitReader.ReadPackedUInt32();

            if (arrayIndexes == 0)
            {
                netBitReader.Seek(netBitReader.GetBitsLeft(), SeekOrigin.Current);

                return null;
            }

            var elementType = fieldInfo.PropertyInfo.PropertyType.GetElementType();
            var replayout = RepLayoutCmdType.Ignore;
            var isGroupType = elementType == groupInfo.Type || elementType == groupInfo.Type.BaseType;

            if (!isGroupType)
            {
                groupInfo = null;

                if (!_parserInfo.PrimitiveTypeLayout.TryGetValue(elementType, out replayout))
                {
                    replayout = RepLayoutCmdType.Ignore;
                }
            }

            var arr = Array.CreateInstance(elementType, arrayIndexes);

            while (true)
            {
                var index = netBitReader.ReadPackedUInt32();

                if (index == 0)
                {
                    if (netBitReader.GetBitsLeft() == 8)
                    {
                        var terminator = netBitReader.ReadPackedUInt32();

                        if (terminator != 0x00)
                        {
                            //Log error

                            return arr;
                        }
                    }

                    return arr;
                }

                --index;

                if (index >= arrayIndexes)
                {
                    //Log error

                    return arr;
                }

                object data = null;

                if (isGroupType)
                {
                    data = _parserInfo.LinqCache.CreateObject(fieldInfo.ElementTypeId);
                }

                while (true)
                {
                    var handle = netBitReader.ReadPackedUInt32();

                    if (handle == 0)
                    {
                        break;
                    }

                    handle--;

                    if (netfieldExportGroup.NetFieldExports.Length < handle)
                    {
                        return arr;
                    }

                    var export = netfieldExportGroup.NetFieldExports[handle];
                    var numBits = netBitReader.ReadPackedUInt32();

                    if (numBits == 0)
                    {
                        continue;
                    }

                    if (export == null)
                    {
                        netBitReader.SkipBits((int)numBits);

                        continue;
                    }

                    try
                    {
                        netBitReader.SetTempEnd((int)numBits, 4);

                        //Uses the same type for the array
                        if (groupInfo != null)
                        {
                            ReadField((NetFieldExportGroupBase)data, export, netfieldExportGroup, handle, netBitReader,
                                false);
                        }
                        else //Probably primitive values
                        {
                            data = ReadDataType(replayout, netBitReader, elementType);
                        }
                    }
                    finally
                    {
                        netBitReader.RestoreTemp(4);
                    }
                }

                arr.SetValue(data, index);
            }
        }

        internal NetFieldExportGroupBase CreateType(int groupId)
        {
            var exportGroup = _parserInfo.NetFieldGroups[groupId];

            var cachedEntry = _objects[exportGroup.TypeId];

            if (cachedEntry.Instance is HandleNetFieldExportGroup handleGroup)
            {
                handleGroup.UnknownHandles.Clear();
            }

            for (var i = 0; i < cachedEntry.ChangedProperties.Count; i++)
            {
                var fieldInfo = cachedEntry.ChangedProperties[i];

                fieldInfo.SetMethod(cachedEntry.Instance, fieldInfo.DefaultValue);
            }

            cachedEntry.ChangedProperties.Clear();

            return cachedEntry.Instance;


            //return _parserInfo.LinqCache.CreateObject(exportGroup.TypeId);
        }

        /// <summary>
        /// Create the object associated with the property that should be read.
        /// Used as a workaround for Rpc structs.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal bool TryCreateRpcPropertyType(string group, string propertyName, out IProperty property)
        {
            property = null;

            if (_parserInfo.NetRpcStructureTypes.TryGetValue(group, out var groupInfo))
            {
                if (groupInfo.PathNames.TryGetValue(propertyName, out var fieldInfo))
                {
                    property = _parserInfo.LinqCache.CreatePropertyObject(fieldInfo.PropertyInfo.PropertyType);

                    return true;
                }
            }

            return false;
        }

        public bool ContainsPath(string path) =>
            _parserInfo.NetFieldGroups.TryGetValue(path, out _) ||
            CoreRedirects.ContainsRedirect(path) ||
            _parserInfo.NetRpcStructureTypes.ContainsKey(path);
    }
}