﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Testing;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.AspNetCore.WebSockets.Server.Test.Autobahn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.AspNetCore.WebSockets.Server.Test
{
    public class AutobahnTests
    {
        // Skip if wstest is not installed for now, see https://github.com/aspnet/WebSockets/issues/95
        // We will enable Wstest on every build once we've gotten the necessary infrastructure sorted out :).
        [ConditionalFact]
        [SkipIfWsTestNotPresent]
        public async Task AutobahnTestSuite()
        {
            var outDir = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "autobahnreports").Replace("\\", "\\\\");
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, recursive: true);
            }

            var spec = new AutobahnSpec(outDir)
                .IncludeCase("*")
                .ExcludeCase("12.*", "13.*");
            var loggerFactory = new LoggerFactory().AddConsole();

            AutobahnResult result;
            using (var tester = new AutobahnTester(loggerFactory, spec))
            {
                await tester.DeployTestAndAddToSpec(ServerType.Kestrel, ssl: false, environment: "ManagedSockets", expectationConfig: expect => expect
                    .NonStrict("6.4.3", "6.4.4")); // https://github.com/aspnet/WebSockets/issues/99

                // Mac + SSL + Untrusted certs == Sadness
                // The macOS version of HttpClient uses Mac's libcurl which doesn't use OpenSSL and doesn't support custom cert validation.
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    await tester.DeployTestAndAddToSpec(ServerType.Kestrel, ssl: true, environment: "ManagedSockets", expectationConfig: expect => expect
                        .NonStrict("6.4.3", "6.4.4")); // https://github.com/aspnet/WebSockets/issues/99
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (IsIISExpress10Installed())
                    {
                        // IIS Express tests are a bit flaky, some tests fail occasionally or get non-strict passes
                        // https://github.com/aspnet/WebSockets/issues/100
                        await tester.DeployTestAndAddToSpec(ServerType.IISExpress, ssl: false, environment: "ManagedSockets", expectationConfig: expect => expect
                            .OkOrFail(Enumerable.Range(1, 20).Select(i => $"5.{i}").ToArray()) // 5.* occasionally fail on IIS express
                            .OkOrFail("9.3.1", "9.4.1")
                            .OkOrNonStrict("3.2", "3.3", "3.4", "4.1.3", "4.1.4", "4.1.5", "4.2.3", "4.2.4", "4.2.5", "5.15")); // These occasionally get non-strict results
                    }

                    if (IsWindows8OrHigher())
                    {
                        await tester.DeployTestAndAddToSpec(ServerType.WebListener, ssl: false, environment: "ManagedSockets", expectationConfig: expect => expect
                            .Fail("6.1.2", "6.1.3") // https://github.com/aspnet/WebSockets/issues/97
                            .Fail("9.7.1", "9.8.1") // https://github.com/aspnet/WebSockets/issues/98
                            .NonStrict("6.4.3", "6.4.4")); // https://github.com/aspnet/WebSockets/issues/99
                    }
                }

                // REQUIRES a build of WebListener that supports native WebSockets, which we don't have right now
                //await tester.DeployTestAndAddToSpec(ServerType.WebListener, ssl: false, environment: "NativeSockets");

                result = await tester.Run();

                tester.Verify(result);
            }
        }

        private bool IsWindows8OrHigher()
        {
            const string WindowsName = "Microsoft Windows ";
            const int VersionOffset = 18;

            if (RuntimeInformation.OSDescription.StartsWith(WindowsName))
            {
                var versionStr = RuntimeInformation.OSDescription.Substring(VersionOffset);
                Version version;
                if (Version.TryParse(versionStr, out version))
                {
                    return version.Major > 6 || (version.Major == 6 && version.Minor >= 2);
                }
            }

            return false;
        }

        private bool IsIISExpress10Installed()
        {
            var pf = Environment.GetEnvironmentVariable("PROGRAMFILES");
            var iisExpressExe = Path.Combine(pf, "IIS Express", "iisexpress.exe");
            return File.Exists(iisExpressExe) && FileVersionInfo.GetVersionInfo(iisExpressExe).FileMajorPart >= 10;
        }
    }
}
