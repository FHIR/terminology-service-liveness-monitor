using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace TestUtility
{
    class TestWebHost
    {
        private const int _port = 8080;

        /// <summary>Gets or sets the status code.</summary>
        public static int StatusCode { get; set; }

        /// <summary>Gets or sets the response body.</summary>
        public static string ResponseBody { get; set; }

        /// <summary>The cancellation token source.</summary>
        private static CancellationTokenSource _cancellationTokenSource = null;
        /// <summary>A token that allows processing to be cancelled.</summary>
        private static CancellationToken _cancellationToken;

        /// <summary>True if is running, false if not.</summary>
        private static bool _isRunning = false;

        /// <summary>Handles the get described by context.</summary>
        /// <param name="context">The context.</param>
        public static async Task HandleGet(HttpContext context)
        {
            if (StatusCode == -1)
            {
                Console.WriteLine("Timing out...");
                await Task.Delay(120 * 1000);
            }
            else
            {
                Console.WriteLine($"Responding with: {StatusCode}: {ResponseBody}");
                context.Response.StatusCode = StatusCode;
                await context.Response.WriteAsync(ResponseBody);
            }
        }

        /// <summary>Starts web host.</summary>
        public static void StartWebHost()
        {
            if (_isRunning)
            {
                _cancellationTokenSource.Cancel();
            }

            // create a cancellation token so we can stop long-running tasks
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            Task.Run(() => CreateHostBuilder().Build().Run(), _cancellationToken);

            _isRunning = true;
        }

        /// <summary>Stops web host.</summary>
        public static void StopWebHost()
        {
            _cancellationTokenSource.Cancel();
            _isRunning = false;
        }

        /// <summary>Creates host builder.</summary>
        /// <returns>The new host builder.</returns>
        private static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls($"http://127.0.0.1:{_port}");
                    webBuilder.UseKestrel();
                    webBuilder.UseStartup<WebHostStartup>();
                });
    }
}
