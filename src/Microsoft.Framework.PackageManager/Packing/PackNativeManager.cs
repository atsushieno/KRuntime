using System;
using System.IO;
using System.Linq;

using Microsoft.Framework.Project;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.Packing
{
    /// <summary>
    /// Generate native image for packages
    /// </summary>
    public class PackNativeManager
    {
        public bool Initialize(PackOptions options)
        {
            if (options.Runtimes.Count() != 1)
            {
                Console.WriteLine("User must provide exactly 1 runtime package when building native images.");
                return false;
            }
            var runtimePath = options.Runtimes.Single();
            var frameworkName = PackManager.DependencyContext.GetFrameworkNameForRuntime(Path.GetFileName(runtimePath));
            // NOTE: !IsDesktop == IsCore and only Core packages can be crossgened at least for now
            if (VersionUtility.IsDesktop(frameworkName))
            {
                Console.WriteLine("Native image generation is currently only supported for KLR Core flavors.");
                return false;
            }

            return true;
        }

        public bool BuildNatives(PackRoot root)
        {
            var runtime = root.Runtimes.Single();
            var runtimeBin = Path.Combine(runtime.TargetPath, "bin");
            var dependencyResolver = new NuGetDependencyResolver(root.PackagesPath, new EmptyFrameworkResolver());
            dependencyResolver.Initialize(root.Packages.Select(p => p.LibraryDescription), runtime.FrameworkName);
            var assemblyPaths = dependencyResolver.PackageAssemblyPaths.Values;
            var assemblyDirs = assemblyPaths.Select(p => Path.GetDirectoryName(p)).Distinct();

            var crossgenOptions = new CrossgenOptions()
            {
                CrossgenPath = Path.Combine(runtimeBin, "crossgen.exe"),
                InputPaths = assemblyDirs,
                RuntimePath = runtimeBin,
                Symbols = false
            };

            var crossgenManager = new CrossgenManager(crossgenOptions);
            return crossgenManager.GenerateNativeImages();
        }
    }
}