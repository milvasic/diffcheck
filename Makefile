.PHONY: run-benchmarks

run-benchmarks:
	dotnet run -c Release --project DiffCheck.Core.Benchmarks -- --filter "*DiffEngine*Benchmarks*"