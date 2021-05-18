# Results Comparer

This simple tool allows for easy comparison of provided benchmark results.

It can be used to rescale:
* historical results (eg. before and after my changes)
* results for different OSes (eg. Windows vs Ubuntu)
* results for different CPU architectures (eg. x64 vs ARM64)
* results for different target frameworks (eg. .NET Core 3.1 vs 5.0)

All you need to provide is:
* `--base` - path to folder/file with baseline results
* `--diff` - path to folder/file with diff results
* `--baselines`  - the list of benchmarks (type | namespace + type | full-name) to use as 'stable-baselines
* `--output`  - path to folder/file with diff results

Optional arguments:
* none so far

Sample: compare the results stored in `C:\results\windows` vs `C:\results\ubuntu` using `StableBaseline` for the comparison, placing the results in the `C:\results\rescaled`.

```cmd
dotnet run --base "C:\results\windows" --diff "C:\results\ubuntu" --baselines "StableBaseline" --output="C:\results\rescaled"
```

**Note**: the tool supports only `*full.json` results exported by BenchmarkDotNet. 

## Sample results

TODO


If there is no difference or if there is no match (we use full benchmark names to match the benchmarks), then the results are omitted.
