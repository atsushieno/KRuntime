// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class AddCommand
    {
        public string Name;
        public string Version;
        public string ProjectDir;
        public IReport Report;

        public bool ExecuteCommand()
        {
            if (string.IsNullOrEmpty(Name))
            {
                Report.WriteLine("Name of dependency to add is required.");
                return false;
            }

            if (string.IsNullOrEmpty(Version))
            {
                Report.WriteLine("Version of dependency to add is required.");
                return false;
            }

            // Only for version validation
            SemanticVersion.Parse(Version);

            ProjectDir = ProjectDir ?? Directory.GetCurrentDirectory();

            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(ProjectDir, out project))
            {
                Report.WriteLine("Unable to locate {0}.", Runtime.Project.ProjectFileName);
                return false;
            }

            var root = JObject.Parse(File.ReadAllText(project.ProjectFilePath));
            if (root["dependencies"] == null)
            {
                root["dependencies"] = new JObject();
            }

            root["dependencies"][Name] = Version;

            File.WriteAllText(project.ProjectFilePath, root.ToString());

            Report.WriteLine("{0}.{1} was added to {2}.", Name, Version, Runtime.Project.ProjectFileName);

            return true;
        }
    }
}
