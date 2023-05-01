using System.Reflection;
using System.Runtime.Loader;

namespace CBot;
class PluginLoadContext : AssemblyLoadContext {
	private readonly AssemblyDependencyResolver resolver;

	public PluginLoadContext(string pluginPath) => this.resolver = new AssemblyDependencyResolver(pluginPath);

	protected override Assembly? Load(AssemblyName assemblyName) {
		var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
		return assemblyPath != null ? this.LoadFromAssemblyPath(assemblyPath) : null;
	}

	protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) {
		var libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
		return libraryPath != null ? this.LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
	}
}
