// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Project;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackManager
    {
        private readonly IServiceProvider _hostServices;
        private readonly PackOptions _options;

        public PackManager(IServiceProvider hostServices, PackOptions options)
        {
            _hostServices = hostServices;
            _options = options;
            _options.ProjectDir = Normalize(_options.ProjectDir);
            ScriptExecutor = new ScriptExecutor();
        }

        public ScriptExecutor ScriptExecutor { get; private set; }

        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }

        public class DependencyContext
        {
            public DependencyContext(string projectDirectory, string configuration, FrameworkName targetFramework)
            {
                var cacheContextAccessor = new CacheContextAccessor();
                var cache = new Cache(cacheContextAccessor);

                var applicationHostContext = new ApplicationHostContext(
                    serviceProvider: null,
                    projectDirectory: projectDirectory,
                    packagesDirectory: null,
                    configuration: configuration,
                    targetFramework: targetFramework,
                    cache: cache,
                    cacheContextAccessor: cacheContextAccessor);

                ProjectResolver = applicationHostContext.ProjectResolver;
                NuGetDependencyResolver = applicationHostContext.NuGetDependencyProvider;
                ProjectReferenceDependencyProvider = applicationHostContext.ProjectDepencyProvider;
                DependencyWalker = applicationHostContext.DependencyWalker;
                FrameworkName = targetFramework;
            }

            public IProjectResolver ProjectResolver { get; set; }
            public NuGetDependencyResolver NuGetDependencyResolver { get; set; }
            public ProjectReferenceDependencyProvider ProjectReferenceDependencyProvider { get; set; }
            public DependencyWalker DependencyWalker { get; set; }
            public FrameworkName FrameworkName { get; set; }

            public void Walk(string projectName, SemanticVersion projectVersion)
            {
                DependencyWalker.Walk(projectName, projectVersion, FrameworkName);
            }

            public static FrameworkName GetFrameworkNameForRuntime(string runtime)
            {
                var parts = runtime.Split(new[] { '.' }, 2);
                if (parts.Length != 2)
                {
                    return null;
                }
                parts = parts[0].Split(new[] { '-' }, 3);
                if (parts.Length != 3)
                {
                    return null;
                }
                switch (parts[1])
                {
                    case "svr50":
                        return VersionUtility.ParseFrameworkName("net451");
                    case "svrc50":
                        return VersionUtility.ParseFrameworkName("k10");
                }
                return null;
            }
        }

        public bool Package()
        {
            if (!ParamsCheck())
            {
                return false;
            }

            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(_options.ProjectDir, out project))
            {
                Console.WriteLine("Unable to locate {0}.'", Runtime.Project.ProjectFileName);
                return false;
            }

            var sw = Stopwatch.StartNew();

            string outputPath = _options.OutputDir ?? Path.Combine(_options.ProjectDir, "bin", "output");

            var projectDir = project.ProjectDirectory;

            var dependencyContexts = new List<DependencyContext>();

            var root = new PackRoot(project, outputPath, _hostServices)
            {
                Overwrite = _options.Overwrite,
                Configuration = _options.Configuration,
                NoSource = _options.NoSource
            };

            Func<string, string> getVariable = key =>
            {
                return null;
            };

            ScriptExecutor.Execute(project, "prepare", getVariable);

            ScriptExecutor.Execute(project, "prepack", getVariable);

            foreach (var runtime in _options.Runtimes)
            {
                var runtimeLocated = TryAddRuntime(root, runtime);

                var kreHome = Environment.GetEnvironmentVariable("KRE_HOME");
                if (string.IsNullOrEmpty(kreHome))
                {
                    kreHome = Environment.GetEnvironmentVariable("ProgramFiles") + @"\KRE;%USERPROFILE%\.kre";
                }

                foreach (var portion in kreHome.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var packagesPath = Path.Combine(
                        Environment.ExpandEnvironmentVariables(portion),
                        "packages",
                        runtime);

                    if (TryAddRuntime(root, packagesPath))
                    {
                        runtimeLocated = true;
                        break;
                    }
                }

                if (!runtimeLocated)
                {
                    Console.WriteLine(string.Format("Unable to locate runtime '{0}'", runtime));
                    return false;
                }

                var frameworkName = DependencyContext.GetFrameworkNameForRuntime(Path.GetFileName(runtime));
                if (!dependencyContexts.Any(dc => dc.FrameworkName == frameworkName))
                {
                    var dependencyContext = new DependencyContext(projectDir, _options.Configuration, frameworkName);
                    dependencyContext.Walk(project.Name, project.Version);
                    dependencyContexts.Add(dependencyContext);
                }
            }

            if (!dependencyContexts.Any())
            {
                var frameworkName = DependencyContext.GetFrameworkNameForRuntime("KRE-svr50-x86.*");
                var dependencyContext = new DependencyContext(projectDir, _options.Configuration, frameworkName);
                dependencyContext.Walk(project.Name, project.Version);
                dependencyContexts.Add(dependencyContext);
            }

            foreach (var dependencyContext in dependencyContexts)
            {
                foreach (var libraryDescription in dependencyContext.NuGetDependencyResolver.Dependencies)
                {
                    if (!root.Packages.Any(p => p.Library == libraryDescription.Identity))
                    {
                        root.Packages.Add(new PackPackage(dependencyContext.NuGetDependencyResolver, libraryDescription));
                    }
                }
                foreach (var libraryDescription in dependencyContext.ProjectReferenceDependencyProvider.Dependencies)
                {
                    if (!root.Projects.Any(p => p.Name == libraryDescription.Identity.Name))
                    {
                        var packProject = new PackProject(dependencyContext.ProjectReferenceDependencyProvider, dependencyContext.ProjectResolver, libraryDescription);
                        if (packProject.Name == project.Name)
                        {
                            packProject.AppFolder = _options.AppFolder;
                        }
                        root.Projects.Add(packProject);
                    }
                }
            }

            root.Emit();

            ScriptExecutor.Execute(project, "postpack", getVariable);

            if (_options.Native)
            {
                var runtimeBin = Path.Combine(root.Runtimes.Select(r => r.TargetPath).Distinct().Single(), "bin");
                // NOTE:
                // 1. k10 will retire
                // 2. Eventually we should find a place that we can keep "aspnetcore" as a constant
                //    We should define a way to identify core lib folders other than hardcoding like this
                var k10PkgDirs = Directory.EnumerateDirectories(root.PackagesPath, "k10", SearchOption.AllDirectories);
                var aspnetCoreDirs = Directory.EnumerateDirectories(root.PackagesPath, "aspnetcore*", SearchOption.AllDirectories);
                var packageDirectories = aspnetCoreDirs.Concat(k10PkgDirs);

                var crossgenOptions = new CrossgenOptions()
                {
                    CrossgenPath = Path.Combine(runtimeBin, "crossgen.exe"),
                    InputPaths = packageDirectories,
                    RuntimePath = runtimeBin,
                    Symbols = false
                };

                var crossgenManager = new CrossgenManager(crossgenOptions);
                if (!crossgenManager.GenerateNativeImages())
                {
                    Console.WriteLine("Native image generation failed.");
                    return false;
                }
            }

            sw.Stop();

            Console.WriteLine("Time elapsed {0}", sw.Elapsed);
            return true;
        }

        bool TryAddRuntime(PackRoot root, string krePath)
        {
            if (!Directory.Exists(krePath))
            {
                return false;
            }

            var kreName = Path.GetFileName(Path.GetDirectoryName(Path.Combine(krePath, ".")));
            var kreNupkgPath = Path.Combine(krePath, kreName + ".nupkg");
            if (!File.Exists(kreNupkgPath))
            {
                return false;
            }

            root.Runtimes.Add(new PackRuntime(kreNupkgPath));
            return true;
        }

        private bool ParamsCheck()
        {
            if (_options.Native)
            {
                if (_options.Runtimes.Count() != 1)
                {
                    Console.WriteLine("User must provide exactly 1 runtime package when building native images.");
                    return false;
                }
                var runtimePath = _options.Runtimes.Single();
                var frameworkName = DependencyContext.GetFrameworkNameForRuntime(Path.GetFileName(runtimePath));
                if (!VersionUtility.IsCore(frameworkName))
                {
                    Console.WriteLine("Native image generation is currently only supported for KLR Core flavors.");
                    return false;
                }
            }

            return true;
        }
    }
}