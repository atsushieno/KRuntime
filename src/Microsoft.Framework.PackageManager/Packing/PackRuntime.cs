// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using NuGet;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackRuntime
    {
        //private readonly NuGetDependencyResolver _nugetDependencyResolver;
        //private readonly Library _library;
        //private readonly FrameworkName _frameworkName;
        string _kreNupkgPath;

        public PackRuntime(
            string kreNupkgPath)
        {
            _kreNupkgPath = kreNupkgPath;
        }

        public string Name { get; set; }
        public SemanticVersion Version { get; set; }
        public string TargetPath { get; set; }

        public FrameworkName FrameworkName
        {
            get { return PackManager.DependencyContext.GetFrameworkNameForRuntime(Path.GetFileName(Path.GetDirectoryName(_kreNupkgPath))); }
        }

        public void Emit(PackRoot root)
        {
            Name = Path.GetFileName(Path.GetDirectoryName(_kreNupkgPath));

            Console.WriteLine("Packing runtime {0}", Name);

            TargetPath = Path.Combine(root.PackagesPath, Name);

            if (Directory.Exists(TargetPath))
            {
                Console.WriteLine("  {0} already exists.", TargetPath);
                return;
            }

            if (!Directory.Exists(TargetPath))
            {
                Directory.CreateDirectory(TargetPath);
            }

            var targetNupkgPath = Path.Combine(TargetPath, Name + ".nupkg");
            using (var sourceStream = File.OpenRead(_kreNupkgPath))
            {
                using (var archive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                {
                    root.Operations.ExtractNupkg(archive, TargetPath);
                }
            }
            using (var sourceStream = File.OpenRead(_kreNupkgPath))
            {
                using (var targetStream = new FileStream(targetNupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sourceStream.CopyTo(targetStream);
                }

                sourceStream.Seek(0, SeekOrigin.Begin);
                var sha512Bytes = SHA512.Create().ComputeHash(sourceStream);
                File.WriteAllText(targetNupkgPath + ".sha512", Convert.ToBase64String(sha512Bytes));
            }
        }

        public override bool Equals(object obj)
        {
            return obj != null && (obj is PackRuntime) && (TargetPath == (obj as PackRuntime).TargetPath);
        }

        public override int GetHashCode()
        {
            return TargetPath == null ? 0 : TargetPath.GetHashCode();
        }
    }
}