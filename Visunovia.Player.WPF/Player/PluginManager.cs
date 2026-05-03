using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Visunovia.Player.WPF.Player;

public class PluginManager
{
    private readonly List<AssemblyInfo> _assemblies = new();
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly Dictionary<string, List<TypeInfo>> _typesByAssembly = new();
    private readonly Dictionary<TypeInfo, List<PluginMethodInfo>> _methodsByType = new();
    private readonly Dictionary<string, object> _instanceCache = new();

    public IReadOnlyList<AssemblyInfo> Assemblies => _assemblies;
    public bool IsLoaded => _assemblies.Count > 0;

    public void LoadPlugins(string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            return;
        }

        var dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll");
        foreach (var dllPath in dllFiles)
        {
            try
            {
                var assemblyName = Path.GetFileNameWithoutExtension(dllPath);
                if (_loadedAssemblies.ContainsKey(assemblyName))
                    continue;

                var assembly = Assembly.LoadFrom(dllPath);
                _loadedAssemblies[assemblyName] = assembly;

                var assemblyInfo = new AssemblyInfo
                {
                    Name = assemblyName,
                    Path = dllPath,
                    Types = new List<TypeInfo>()
                };

                var types = assembly.DefinedTypes.Where(t => t.IsClass && t.IsPublic).ToList();
                foreach (var type in types)
                {
                    var typeInfo = new TypeInfo
                    {
                        FullName = type.FullName ?? type.Name,
                        Constructors = new List<ConstructorInfo>(),
                        Methods = new List<PluginMethodInfo>()
                    };

                    var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var ctor in constructors)
                    {
                        typeInfo.Constructors.Add(new ConstructorInfo
                        {
                            Parameters = ctor.GetParameters().Select(p => new ParameterInfo
                            {
                                Name = p.ParameterType.Name,
                                TypeName = p.ParameterType.FullName ?? p.ParameterType.Name
                            }).ToList()
                        });
                    }

                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .Where(m => !m.IsSpecialName && m.DeclaringType == type)
                        .ToList();

                    foreach (var method in methods)
                    {
                        typeInfo.Methods.Add(new PluginMethodInfo
                        {
                            Name = method.Name,
                            Parameters = method.GetParameters().Select(p => new ParameterInfo
                            {
                                Name = p.ParameterType.Name,
                                TypeName = p.ParameterType.FullName ?? p.ParameterType.Name
                            }).ToList()
                        });
                    }

                    _methodsByType[typeInfo] = methods.Select(m => new PluginMethodInfo { Name = m.Name, Parameters = m.GetParameters().Select(p => new ParameterInfo { Name = p.ParameterType.Name, TypeName = p.ParameterType.FullName ?? p.ParameterType.Name }).ToList() }).ToList();
                    assemblyInfo.Types.Add(typeInfo);
                    _typesByAssembly[assemblyName] = assemblyInfo.Types;
                }

                _assemblies.Add(assemblyInfo);
            }
            catch
            {
            }
        }
    }

    public List<TypeInfo> GetTypes(string assemblyName)
    {
        if (_typesByAssembly.TryGetValue(assemblyName, out var types))
        {
            return types;
        }
        return new List<TypeInfo>();
    }

    public List<PluginMethodInfo> GetMethods(string assemblyName, string typeFullName)
    {
        foreach (var assemblyInfo in _assemblies)
        {
            if (assemblyInfo.Name != assemblyName)
                continue;

            foreach (var type in assemblyInfo.Types)
            {
                if (type.FullName == typeFullName)
                {
                    return type.Methods;
                }
            }
        }
        return new List<PluginMethodInfo>();
    }

    public List<ConstructorInfo> GetConstructors(string assemblyName, string typeFullName)
    {
        foreach (var assemblyInfo in _assemblies)
        {
            if (assemblyInfo.Name != assemblyName)
                continue;

            foreach (var type in assemblyInfo.Types)
            {
                if (type.FullName == typeFullName)
                {
                    return type.Constructors;
                }
            }
        }
        return new List<ConstructorInfo>();
    }

    public object? CreateInstance(string assemblyName, string typeFullName, Dictionary<string, object> constructorArgs)
    {
        if (!_loadedAssemblies.TryGetValue(assemblyName, out var assembly))
            return null;

        var type = assembly.GetTypes().FirstOrDefault(t => t.FullName == typeFullName);
        if (type == null)
            return null;

        try
        {
            var cacheKey = $"{assemblyName}:{typeFullName}";

            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length == 0 && (constructorArgs == null || constructorArgs.Count == 0))
                {
                    var instance = Activator.CreateInstance(type);
                    _instanceCache[cacheKey] = instance;
                    return instance;
                }

                if (parameters.Length == constructorArgs?.Count)
                {
                    var args = new object[parameters.Length];
                    var canCreate = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (constructorArgs.TryGetValue(parameters[i].Name, out var argValue))
                        {
                            args[i] = ConvertParameter(argValue, parameters[i].ParameterType);
                        }
                        else
                        {
                            canCreate = false;
                            break;
                        }
                    }

                    if (canCreate)
                    {
                        var instance = ctor.Invoke(args);
                        _instanceCache[cacheKey] = instance;
                        return instance;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public bool InvokeMethod(string assemblyName, string typeFullName, string methodName, object? instance = null)
    {
        if (!_loadedAssemblies.TryGetValue(assemblyName, out var assembly))
            return false;

        var type = assembly.GetTypes().FirstOrDefault(t => t.FullName == typeFullName);
        if (type == null)
            return false;

        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
            return false;

        try
        {
            if (method.IsStatic)
            {
                method.Invoke(null, null);
            }
            else
            {
                var targetInstance = instance ?? _instanceCache.GetValueOrDefault($"{assemblyName}:{typeFullName}");
                if (targetInstance == null)
                    return false;
                method.Invoke(targetInstance, null);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool InvokeMethodWithInstance(string assemblyName, string typeFullName, string methodName, object targetInstance)
    {
        if (!_loadedAssemblies.TryGetValue(assemblyName, out var assembly))
            return false;

        var type = assembly.GetTypes().FirstOrDefault(t => t.FullName == typeFullName);
        if (type == null)
            return false;

        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
            return false;

        try
        {
            method.Invoke(targetInstance, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private object ConvertParameter(object value, Type targetType)
    {
        if (value == null)
            return null!;

        if (targetType == typeof(int))
            return Convert.ToInt32(value);
        if (targetType == typeof(double))
            return Convert.ToDouble(value);
        if (targetType == typeof(float))
            return Convert.ToSingle(value);
        if (targetType == typeof(bool))
            return Convert.ToBoolean(value);
        if (targetType == typeof(string))
            return value.ToString() ?? "";

        return value;
    }

    public object? GetCachedInstance(string assemblyName, string typeFullName)
    {
        var key = $"{assemblyName}:{typeFullName}";
        return _instanceCache.GetValueOrDefault(key);
    }

    public void ClearCache()
    {
        _instanceCache.Clear();
    }

    public void Clear()
    {
        _assemblies.Clear();
        _loadedAssemblies.Clear();
        _typesByAssembly.Clear();
        _methodsByType.Clear();
        _instanceCache.Clear();
    }
}

public class AssemblyInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public List<TypeInfo> Types { get; set; } = new();
}

public class TypeInfo
{
    public string FullName { get; set; } = "";
    public List<ConstructorInfo> Constructors { get; set; } = new();
    public List<PluginMethodInfo> Methods { get; set; } = new();
}

public class ConstructorInfo
{
    public List<ParameterInfo> Parameters { get; set; } = new();
}

public class PluginMethodInfo
{
    public string Name { get; set; } = "";
    public List<ParameterInfo> Parameters { get; set; } = new();
}

public class ParameterInfo
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
}
