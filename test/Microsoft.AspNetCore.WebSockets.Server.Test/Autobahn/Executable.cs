using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.WebSockets.Server.Test.Autobahn
{
    public class Executable
    {
        private static readonly string _exeSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        private readonly string _path;

        protected Executable(string path)
        {
            _path = path;
        }

        public static string Locate(string name)
        {
            foreach (var dir in Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(dir, name + _exeSuffix);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        public Task<Result> ExecAsync(string args)
        {
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = _path,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();
            var tcs = new TaskCompletionSource<Result>();

            process.OutputDataReceived += (_, a) => stdOut.Append(a.Data);
            process.ErrorDataReceived += (_, a) => stdErr.Append(a.Data);
            process.Exited += (_, __) => tcs.TrySetResult(new Result(process.ExitCode, stdOut.ToString(), stdErr.ToString()));

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }

        public struct Result
        {
            public int ExitCode { get; }
            public string StdOut { get; }
            public string StdErr { get; }

            public Result(int exitCode, string stdOut, string stdErr)
            {
                ExitCode = exitCode;
                StdOut = stdOut;
                StdErr = stdErr;
            }
        }
    }
}
