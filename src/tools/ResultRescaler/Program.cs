// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Perfolizer.Mathematics.Multimodality;
using CommandLine;
using DataTransferContracts;
using MarkdownLog;
using Newtonsoft.Json;

namespace ResultsComparer
{
    public class Program
    {
        private const string FullBdnJsonFileExtension = "full.json";
        private const string RescaledBdnJsonFileExtension = "rescaled.json";

        public static void Main(string[] args)
        {
            // we print a lot of numbers here and we want to make it always in invariant way
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(Compare);
        }

        private static void Compare(CommandLineOptions args)
        {
            var notSame = RescaleResults(args);

            if (!notSame.Any())
            {
                Console.WriteLine($"No differences found between the benchmark results.");
                return;
            }

            PrintSummary(notSame);

            PrintTable(notSame, args);
        }
        

        private static void PrintSummary((string id, Benchmark baseResult, Benchmark diffResult)[] notSame)
        {
            var better = notSame.Select(GetRatio).Where(x=> x > 1 && !double.IsPositiveInfinity(x)).ToList();
            var worse = notSame.Select(GetRatio).Where(x => x < 1 && !double.IsNegativeInfinity(x)).ToList();
            var betterCount = better.Count;
            var worseCount = worse.Count;
            
            Console.WriteLine("summary:");

            if (betterCount > 0)
            {
                var betterGeoMean = Math.Pow(10, better.Skip(1).Aggregate(Math.Log10(better.First()), (x, y) => x + Math.Log10(y)) / better.Count);
                Console.WriteLine($"better: {betterCount}, geomean: {betterGeoMean:F3}");
            }

            if (worseCount > 0)
            {
                var worseGeoMean = Math.Pow(10, worse.Skip(1).Aggregate(Math.Log10(worse.First()), (x, y) => x + Math.Log10(y)) / worse.Count);
                Console.WriteLine($"worse: {worseCount}, geomean: {worseGeoMean:F3}");
            }

            Console.WriteLine($"total diff: {notSame.Length}");
            Console.WriteLine();
        }

        private static double GetRatio((string id, Benchmark baseResult, Benchmark diffResult) result) => GetRatio(result.baseResult, result.diffResult);

        private static void PrintTable((string id, Benchmark baseResult, Benchmark diffResult)[] notSame, CommandLineOptions args)
        {
            var data = notSame
                .OrderByDescending(result => GetRatio(result.baseResult, result.diffResult))
                .Select(result => new
                {
                    Id = result.id,
                    DisplayValue = GetRatio( result.baseResult, result.diffResult),
                    BaseMedian = result.baseResult.Statistics.Median,
                    DiffMedian = result.diffResult.Statistics.Median,
                    Modality = GetModalInfo(result.baseResult) ?? GetModalInfo(result.diffResult)
                })
                .ToArray();

            if (!data.Any())
            {
                Console.WriteLine($"No matching results for the provided baselines = {string.Join(", ", args.Baselines)}");
                Console.WriteLine();
                return;
            }

            var table = data.ToMarkdownTable().WithHeaders("base/diff", "Base Median (ns)", "Diff Median (ns)", "Modality");

            foreach (var line in table.ToMarkdown().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                Console.WriteLine($"| {line.TrimStart()}|"); // the table starts with \t and does not end with '|' and it looks bad so we fix it

            Console.WriteLine();
        }

        private static (string id, Benchmark baseResult, Benchmark diffResult)[] RescaleResults(CommandLineOptions args)
        {
            var baseFiles = GetFilesToParse(args.BasePath);
            var diffFiles = GetFilesToParse(args.DiffPath);

            if (!baseFiles.Any() || !diffFiles.Any())
                throw new ArgumentException($"Provided paths contained no {FullBdnJsonFileExtension} files.");

            var baseResults = baseFiles.Select(ReadFromFile);
            var diffResults = diffFiles.ToDictionary(Path.GetFileName, ReadFromFile);

            var benchmarkIdToDiffResults = diffResults.Values
                .SelectMany(result => result.Benchmarks.Where(x => x.Statistics != null))
                .Where(benchmarkResult => args.Baselines.Contains(benchmarkResult.MethodTitle))
                .ToDictionary(benchmarkResult => benchmarkResult.FullName, benchmarkResult => benchmarkResult);

            var baselineResults = baseResults
                .SelectMany(result => result.Benchmarks.Where(x => x.Statistics != null))
                .ToDictionary(benchmarkResult => benchmarkResult.FullName, benchmarkResult => benchmarkResult) // we use ToDictionary to make sure the results have unique IDs
                .Where(baseResult => benchmarkIdToDiffResults.ContainsKey(baseResult.Key))
                .Select(baseResult => (baseResult.Key, baseResult.Value, benchmarkIdToDiffResults[baseResult.Key])).ToArray();

            if (baselineResults.Length == 0)
                return baselineResults;

            var scaleFactor = baselineResults.Average(GetRatio);
            
            foreach (var diffResult in diffResults) 
            {
                foreach (var benchmark in diffResult.Value.Benchmarks)
                {
                    benchmark.RescaleValues(scaleFactor);
                }

                WriteToFile(diffResult.Value, Path.Combine(args.OutputPath, diffResult.Key.Replace(FullBdnJsonFileExtension, RescaledBdnJsonFileExtension)));
            }

            return baselineResults;
        }

        private static string[] GetFilesToParse(string path)
        {
            if (Directory.Exists(path))
                return Directory.GetFiles(path, $"*{FullBdnJsonFileExtension}", SearchOption.AllDirectories);
            if (File.Exists(path) && path.EndsWith(FullBdnJsonFileExtension))
                return new[] { path };
            throw new FileNotFoundException($"Provided path does NOT exist or is not a {path} file", path);
        }

        // code and magic values taken from BenchmarkDotNet.Analysers.MultimodalDistributionAnalyzer
        // See http://www.brendangregg.com/FrequencyTrails/modes.html
        private static string GetModalInfo(Benchmark benchmark)
        {
            if (benchmark.Statistics.N < 12) // not enough data to tell
                return null;

            double mValue = MValueCalculator.Calculate(benchmark.GetOriginalValues());
            if (mValue > 4.2)
                return "multimodal";
            else if (mValue > 3.2)
                return "bimodal";
            else if (mValue > 2.8)
                return "several?";

            return null;
        }

        private static double GetRatio(Benchmark baseResult, Benchmark diffResult) => baseResult.Statistics.Median / diffResult.Statistics.Median;

        private static BdnResult ReadFromFile(string resultFilePath)
        {
            try
            {
                return JsonConvert.DeserializeObject<BdnResult>(File.ReadAllText(resultFilePath));
            }
            catch (JsonSerializationException)
            {
                Console.WriteLine($"Exception while reading the {resultFilePath} file.");

                throw;
            }
        }
        private static void WriteToFile(BdnResult result, string resultFilePath)
        {
            try
            {
                var directoryInfo = new FileInfo(resultFilePath).Directory;
                if (directoryInfo != null)
                {
                    directoryInfo.Create();
                }

                File.WriteAllText(resultFilePath, JsonConvert.SerializeObject(result));
            }
            catch (JsonSerializationException)
            {
                Console.WriteLine($"Exception while writing to the output file path: {resultFilePath}.");

                throw;
            }
        }
    }
}
