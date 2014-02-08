﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Common;

namespace Microsoft.Net.Project
{
    public class Program
    {
        public int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("k [command] [application]");
                return -1;
            }

            string command = args[0];
            string application = args.Length >= 2 ? args[1] : Directory.GetCurrentDirectory();

            var projectManager = new ProjectManager(application);

            try
            {
                if (command.Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                    if (!projectManager.Build())
                    {
                        return -1;
                    }
                }
                else if (command.Equals("clean", StringComparison.OrdinalIgnoreCase))
                {
                    if (!projectManager.Clean())
                    {
                        return -1;
                    }
                }
                else
                {
                    Console.WriteLine("unknown command '{0}'", command);
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(String.Join(Environment.NewLine, ExceptionHelper.GetExceptions(ex)));
                return -2;
            }

            return 0;
        }
    }
}
