// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.PackageManager;

namespace NuGet
{
    public interface ILocalPackageRepository
    {
        string Source { get; }

        IPackagePathResolver PathResolver { get; set; }

        IFileSystem FileSystem { get; }

        IReport Report { get; set; }

        IPackage FindPackage(string packageId, SemanticVersion version);

        IEnumerable<IPackage> FindPackagesById(string packageId);
    }
}