// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using CommandLine;
using CommandLine.Text;

namespace ResultsComparer
{
    public class CommandLineOptions
    {
        [Option("base", HelpText = "Path to the folder/file with base results.")]
        public string BasePath { get; set; }

        [Option("diff", HelpText = "Path to the folder/file with diff results.")]
        public string DiffPath { get; set; }

        [Option('o', "output", HelpText = "The folder to store the rescaled JSON results.")]
        public string OutputPath { get; set; }

        [Option('b', "baselines", HelpText = "The list of benchmarks (type | namespace + type | full-name) to use as 'stable-baselines'.")]
        public IEnumerable<string> Baselines { get; set; }

        [Option('m', "medians", HelpText = "Use the medians for determining the scale factor (default is 'means').")]
        public bool UseMedians { get; set; }


        [Usage(ApplicationAlias = "")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(@"Compare the results stored in 'C:\results\win' (base) vs 'C:\results\unix' (diff) using 5% threshold.",
                    new CommandLineOptions { BasePath = @"C:\results\win", DiffPath = @"C:\results\unix", Baselines = new []{"StableBaseline"}, OutputPath =  @"C:\results\unix\rescaled" });
            }
        }
    }
}