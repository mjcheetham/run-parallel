using System.Diagnostics;
using System.CommandLine;
using System.CommandLine.Invocation;
using static System.Console;

var numOpt = new Option<int>(new[] { "--num", "-n" }, "Number of processes to run in parallel")
{
    Arity = ArgumentArity.ExactlyOne,
    IsRequired = true
};

var envOpt = new Option<string[]>(new[] { "--env", "-e" }, "Environment variable to set (use {i} to substitute the process number)")
{
    Arity = ArgumentArity.ZeroOrMore
};

var exeArg = new Argument<string>("executable", "Executable to run")
{
    Arity = ArgumentArity.ExactlyOne
};

var rootCommand = new RootCommand
{
    TreatUnmatchedTokensAsErrors = false
};

rootCommand.Add(numOpt);
rootCommand.Add(envOpt);
rootCommand.Add(exeArg);
rootCommand.SetHandler(ExecuteAsync);

return await rootCommand.InvokeAsync(args);

async Task<int> ExecuteAsync(InvocationContext context)
{
    int numProcesses = context.ParseResult.GetValueForOption(numOpt);
    string[] envOptions = context.ParseResult.GetValueForOption(envOpt) ?? Array.Empty<string>();
    string executable = context.ParseResult.GetValueForArgument(exeArg);
    string executableArgs = string.Join(' ', context.ParseResult.UnmatchedTokens);

    var processes = new Process[numProcesses];
    for (int i = 0; i < numProcesses; i++)
    {
        string exitMessage = $"Process {i} exited.";
        var psi = new ProcessStartInfo(executable, executableArgs);
        foreach (string env in envOptions)
        {
            string[] kvp = env.Split('=', 2);
            if (kvp.Length == 2)
            {
                psi.EnvironmentVariables[kvp[0]] = kvp[1].Replace("{i}", i.ToString());
            }
            else if (kvp.Length == 1 && psi.EnvironmentVariables.ContainsKey(kvp[0]))
            {
                psi.EnvironmentVariables.Remove(kvp[0]);
            }
        }
        processes[i] = new Process { StartInfo = psi };
        processes[i].Exited += (sender, e) => Error.WriteLine(exitMessage);
    }

    Error.WriteLine($"Starting {numProcesses} processes...");

    var tasks = new Task[numProcesses];
    Parallel.For(0, numProcesses, new ParallelOptions { MaxDegreeOfParallelism = numProcesses },
        i =>
        {
            processes[i].Start();
            Error.WriteLine($"Process {i} started.");
            tasks[i] = processes[i].WaitForExitAsync();
        }
    );

    await Task.WhenAll(tasks);
    Error.WriteLine("All processes exited.");

    return 0;
}
