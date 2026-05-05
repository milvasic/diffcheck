.PHONY: run-benchmarks

format:
	dotnet csharpier format .

run-benchmarks:
	dotnet run -c Release --project DiffCheck.Core.Benchmarks -- --filter "*DiffEngine*Benchmarks*"