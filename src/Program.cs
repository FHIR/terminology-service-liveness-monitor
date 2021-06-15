using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;

namespace terminology_service_liveness_monitor
{
    /// <summary>Main class for a simple Windows Service monitoring service.</summary>
    public static class Program
    {
        /// <summary>Gets or sets the configuration.</summary>
        public static IConfiguration Configuration { get; set; }

        private static CancellationTokenSource _cancellationTokenSource;
        private static CancellationToken _cancellationToken;


        /// <summary>A service for accessing windows information.</summary>
        private class WindowsService : ServiceBase
        {
            /// <summary>
            /// When implemented in a derived class, executes when a Start command is sent to the service by
            /// the Service Control Manager (SCM) or when the operating system starts (for a service that
            /// starts automatically). Specifies actions to take when the service starts.
            /// </summary>
            /// <param name="args">Data passed by the start command.</param>
            protected override void OnStart(string[] args)
            {
                Program.Start();
            }

            /// <summary>
            /// When implemented in a derived class, executes when a Stop command is sent to the service by
            /// the Service Control Manager (SCM). Specifies actions to take when a service stops running.
            /// </summary>
            protected override void OnStop()
            {
                Program.Stop();
            }
        }

        /// <summary>A program that monitors windows services.</summary>
        /// <returns>Exit-code for the process - 0 for success, else an error code.</returns>
        public static int Main()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Console.WriteLine($"Windows Service Monitor is unsupported on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
                return -1;
            }

            // configuration ordering: command line > environment vars > json file 
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // ensure we have a service name and url
            if (string.IsNullOrEmpty(Configuration["WindowsServiceName"]))
            {
                throw new ArgumentNullException("WindowsServiceName is required!");
            }

            if (string.IsNullOrEmpty(Configuration["ServiceTestUrl"]))
            {
                throw new ArgumentNullException("ServiceTestUrl is required!");
            }

            // create a cancellation token so we can stop long-running tasks
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            if (!Environment.UserInteractive)
            {
                // start our service
                using (WindowsService monitorService = new WindowsService())
                {
                    ServiceBase.Run(monitorService);
                }
            }
            else
            {
                // run as console app
                Start();

                System.Console.WriteLine("Press enter to exit...");
                System.Console.ReadLine();

                Stop();
            }

            return 0;
        }

        internal static void Start()
        {
            // create our service host
            CreateHostBuilder().Build().RunAsync(_cancellationToken);
        }

        internal static void Stop()
        {
            Console.WriteLine("Stopping...");
            _cancellationTokenSource.Cancel();
        }

        /// <summary>Creates host builder.</summary>
        /// <returns>The new host builder.</returns>
        public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<HostedMonitoringService>();
                });
    }
}