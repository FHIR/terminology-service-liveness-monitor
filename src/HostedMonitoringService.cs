using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace terminology_service_liveness_monitor
{
    class HostedMonitoringService : IHostedService, IDisposable
    {
        /// <summary>The callback timer.</summary>
        private Timer _timer;
        private bool _disposedValue;

        private string _serviceName;
        private string _processName;
        private string _serviceUrl;
        private int _serviceStopDelayMs;

        /// <summary>
        /// Initializes a new instance of the terminiology-service-liveness-monitor.HostedMonitoringService class.
        /// </summary>
        public HostedMonitoringService()
        {
            _disposedValue = false;
        }

        /// <summary>Check service and restart if needed.</summary>
        /// <param name="state">The state.</param>
        private async void CheckServiceAndRestartIfNeeded(object state)
        {
            HttpClient client = new HttpClient();

            try
            {
                HttpResponseMessage response = await client.SendAsync(
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        _serviceUrl));

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Successful response: {DateTime.Now}");
                    return;
                }

                Console.WriteLine($"Request to {_serviceUrl} failed: {response.StatusCode}!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request to {_serviceUrl} failed: {ex.Message}");
            }

            ServiceController sc = new ServiceController(_serviceName);

            switch (sc.Status)
            {
                case ServiceControllerStatus.Running:
                    Console.WriteLine($"Stopping service {_serviceName} after failed request");
                    
                    try
                    {
                        sc.Stop();
                        await Task.Delay(_serviceStopDelayMs).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to STOP service {_serviceName}: {ex.Message}");
                    }

                    break;

                // starting up, figure out next loop
                case ServiceControllerStatus.StartPending:
                    Console.WriteLine($"Service {_serviceName} is starting up, will check next loop...");
                    return;

                // states should only be possible manually, don't mess with the user
                case ServiceControllerStatus.ContinuePending:
                case ServiceControllerStatus.PausePending:
                case ServiceControllerStatus.Paused:
                    Console.WriteLine($"Service {_serviceName} in manual state {sc.Status} - ignoring...");
                    return;

                case ServiceControllerStatus.Stopped:
                case ServiceControllerStatus.StopPending:
                default:
                    break;
            }

            if (!string.IsNullOrEmpty(_processName))
            {
                Process[] processes = Process.GetProcesses();
                foreach (Process proc in processes)
                {
                    if (proc.ProcessName == _processName)
                    {
                        Console.WriteLine($"Still found process: {_processName}, killing...");
                        proc.Kill();
                        break;
                    }
                }
            }

            Console.WriteLine($"Starting service {_serviceName}...");

            sc.Refresh();

            try
            {
                sc.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to START service {_serviceName}: {ex.Message}");
            }
        }

        /// <summary>Triggered when the application host is ready to start the service.</summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns>An asynchronous result.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Hosted Monitoring Service Started.");

            _serviceName = Program.Configuration["WindowsServiceName"];
            _processName = Program.Configuration["ProcessName"];
            _serviceUrl = Program.Configuration["ServiceTestUrl"];

            if ((!string.IsNullOrEmpty(Program.Configuration["Email:SMTP_Server"])) &&
                (!string.IsNullOrEmpty(Program.Configuration["Email:From"])) &&
                (!string.IsNullOrEmpty(Program.Configuration["Email:To"])) &&
                (!string.IsNullOrEmpty(Program.Configuration["Email:SMTP_User"])) &&
                (!string.IsNullOrEmpty(Program.Configuration["Email:SMTP_Password"])))

            if (int.TryParse(Program.Configuration["ServiceStopDelaySeconds"], out int delaySeconds))
            {
                _serviceStopDelayMs = delaySeconds * 1000;
            }
            else
            {
                _serviceStopDelayMs = 10 * 1000;
            }

            string intervalSecondString = Program.Configuration["PollIntervalSeconds"];
            int seconds;

            if (!int.TryParse(intervalSecondString, out seconds))
            {
                seconds = 30;
            }

            while ((seconds * 1000) < _serviceStopDelayMs)
            {
                seconds += 5;
            }

            _timer = new Timer(
                CheckServiceAndRestartIfNeeded,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(seconds));

            return Task.CompletedTask;
        }

        /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
        ///  graceful.</param>
        /// <returns>An asynchronous result.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the
        /// argonaut_subscription_server_proxy.Services.WebsocketHeartbeatService and optionally releases
        /// the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to
        ///  release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
        /// resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
