using System.Reflection;
using System.Runtime.Loader;
using TV_Player.Plugins;

namespace TV_Player.Providers;

/// <summary>Loads trusted channel-provider plugins and creates configured provider instances.</summary>
public sealed class ChannelProviderRegistry : IDisposable
{
    private readonly Dictionary<string, IChannelProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IChannelProviderPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PluginLoadContext> _loadContexts = new();
    private readonly List<string> _loadErrors = new();

    public IReadOnlyCollection<IChannelProvider> Providers => _providers.Values.ToArray();
    public IReadOnlyCollection<IChannelProviderPlugin> Plugins => _plugins.Values.ToArray();
    public IReadOnlyList<string> LoadErrors => _loadErrors.AsReadOnly();

    public void Register(IChannelProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (string.IsNullOrWhiteSpace(provider.Id))
            throw new ArgumentException("Provider ID cannot be empty.", nameof(provider));
        if (!_providers.TryAdd(provider.Id, provider))
            throw new InvalidOperationException($"Provider '{provider.Id}' is already registered.");
    }

    public bool TryGet(string providerId, out IChannelProvider? provider) =>
        _providers.TryGetValue(providerId, out provider);

    public bool TryGetPlugin(string pluginId, out IChannelProviderPlugin? plugin) =>
        _plugins.TryGetValue(pluginId, out plugin);

    public void LoadPlugins(params string[] pluginsDirectories)
    {
        ArgumentNullException.ThrowIfNull(pluginsDirectories);
        var searchedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pluginsDirectory in pluginsDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            Directory.CreateDirectory(pluginsDirectory);
            foreach (var assemblyPath in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories))
            {
                if (!searchedAssemblies.Add(Path.GetFullPath(assemblyPath)))
                    continue;

                PluginLoadContext? loadContext = null;
                try
                {
                    loadContext = new PluginLoadContext(assemblyPath);
                    var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
                    var pluginTypes = GetPluginTypes(assembly);
                    if (pluginTypes.Count == 0)
                    {
                        loadContext.Unload();
                        continue;
                    }

                    var loadedPluginCount = 0;
                    foreach (var pluginType in pluginTypes)
                    {
                        if (Activator.CreateInstance(pluginType) is not IChannelProviderPlugin plugin)
                        {
                            _loadErrors.Add($"{assemblyPath}: {pluginType.FullName} needs a public parameterless constructor.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(plugin.Id) || string.IsNullOrWhiteSpace(plugin.Name))
                        {
                            _loadErrors.Add($"{assemblyPath}: plugin ID and name are required.");
                            continue;
                        }

                        if (!_plugins.TryAdd(plugin.Id, plugin))
                        {
                            _loadErrors.Add($"{assemblyPath}: plugin ID '{plugin.Id}' is already loaded.");
                            continue;
                        }

                        loadedPluginCount++;
                    }

                    if (loadedPluginCount > 0)
                        _loadContexts.Add(loadContext);
                    else
                        loadContext.Unload();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    loadContext?.Unload();
                    var details = string.Join("; ", ex.LoaderExceptions.Where(error => error != null).Select(error => error!.Message));
                    _loadErrors.Add($"{assemblyPath}: could not load plugin types. {details}");
                }
                catch (Exception ex)
                {
                    loadContext?.Unload();
                    _loadErrors.Add($"{assemblyPath}: {ex.Message}");
                }
            }
        }
    }

    public IChannelProvider CreateProvider(ProviderSourceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!_plugins.TryGetValue(configuration.PluginId, out var plugin))
            throw new KeyNotFoundException($"Provider plugin '{configuration.PluginId}' is not loaded.");

        var provider = plugin.CreateProvider(configuration)
            ?? throw new InvalidOperationException($"Plugin '{plugin.Id}' returned no provider.");
        if (!string.Equals(provider.Id, configuration.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Plugin '{plugin.Id}' must create a provider whose ID matches the configured source ID.");
        return provider;
    }

    private static IReadOnlyList<Type> GetPluginTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IChannelProviderPlugin).IsAssignableFrom(type))
                .ToArray();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types
                .OfType<Type>()
                .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IChannelProviderPlugin).IsAssignableFrom(type))
                .ToArray();
        }
    }

    public void Dispose()
    {
        foreach (var loadContext in _loadContexts)
            loadContext.Unload();

        _loadContexts.Clear();
        _plugins.Clear();
        _providers.Clear();
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private static readonly string ContractAssemblyName = typeof(IChannelProviderPlugin).Assembly.GetName().Name!;

        public PluginLoadContext(string pluginAssemblyPath)
            : base($"TVPlayerPlugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}:{Guid.NewGuid():N}", isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(Path.GetFullPath(pluginAssemblyPath));
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Share the contract types with the host so plugin interfaces have one identity.
            if (string.Equals(assemblyName.Name, ContractAssemblyName, StringComparison.OrdinalIgnoreCase))
                return null;

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath == null ? null : LoadFromAssemblyPath(assemblyPath);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
        }
    }
}
