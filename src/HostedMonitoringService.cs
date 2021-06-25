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
    /// <summary>A .Net service for monitoring Windows services.</summary>
    public class HostedMonitoringService : IHostedService, IDisposable
    {
        /// <summary>The callback timer.</summary>
        private Timer _timer;

        /// <summary>Dispose state tracking (IDisposable).</summary>
        private bool _disposedValue;

        /// <summary>Name of the service we are monitoring.</summary>
        private string _serviceName;

        /// <summary>Name of the process we are monitoring.</summary>
        private string _processName;

        /// <summary>URL of the service we are monitoring.</summary>
        private string _serviceUrl;

        /// <summary>The amount of time to wait after stopping a service.</summary>
        private int _serviceStopDelayMs;

        /// <summary>True to kill a process if the service takes too long to stop.</summary>
        private bool _killProcess;

        /// <summary>True if monitoring is active.</summary>
        private bool _monitoringIsActive;

        /// <summary>
        /// Initializes a new instance of the terminiology-service-liveness-monitor.HostedMonitoringService class.
        /// </summary>
        public HostedMonitoringService()
        {
            _disposedValue = false;
            _monitoringIsActive = false;
        }

        /// <summary>Occurs when Stopping Monitored Service.</summary>
        public event EventHandler StoppingMonitoredService;

        /// <summary>Occurs when Stopped Monitored Service.</summary>
        public event EventHandler StoppedMonitoredService;

        /// <summary>Occurs when Starting Monitored Service.</summary>
        public event EventHandler StartingMonitoredService;

        /// <summary>Occurs when Started Monitored Service.</summary>
        public event EventHandler StartedMonitoredService;

        /// <summary>Occurs when Service Test Failed.</summary>
        public event EventHandler ServiceTestFailed;

        /// <summary>Occurs when Service Test Passed.</summary>
        public event EventHandler ServiceTestPassed;

        /// <summary>Occurs when Service Test is waiting for the service to become active.</summary>
        public event EventHandler ServiceTestWaitingStart;

        /// <summary>Raises the stopping monitored service event.</summary>
        /// <param name="e">Event information to send to registered event handlers.</param>
        protected virtual void OnStoppingMonitoredService(EventArgs e = null)
        {
            Console.WriteLine($"Stopping service {_serviceName}...");
            StoppingMonitoredService?.Invoke(this, e);
        }

        /// <summary>Raises the stopped monitored service event.</summary>
        /// <param name="e">Event information to send to registered event handlers.</param>
        protected virtual void OnStoppedMonitoredService(EventArgs e = null)
        {
            StoppedMonitoredService?.Invoke(this, e);
        }

        /// <summary>Raises the starting monitored service event.</summary>
        /// <param name="e">Event information to send to registered event handlers.</param>
        protected virtual void OnStartingMonitoredService(EventArgs e = null)
        {
            Console.WriteLine($"Starting service {_serviceName}...");
            StartingMonitoredService?.Invoke(this, e);
        }

        /// <summary>Raises the started monitored service event.</summary>
        /// <param name="e">Event information to send to registered event handlers.</param>
        protected virtual void OnStartedMonitoredService(EventArgs e = null)
        {
            StartedMonitoredService?.Invoke(this, e);
        }

        /// <summary>Raises the service test failed event.</summary>
        /// <param name="e">Event information to send to registered event handlers.</param>
        protected virtual void OnServiceTestFailed(EventArgs e = null)
        {
            ServiceTestFailed?.Invoke(this, e);
        }

        /// <summary>Raises the service test waiting event.</summary>
        /// <param name="e">Event information to send to registered event handlers.</param>
        protected virtual void OnServiceTestWaitingStart(EventArgs e = null)
        {
            Console.WriteLine($"Waiting for initial success to begin monitoring, service: {_serviceName}, url: {_serviceUrl}");
            ServiceTestWaitingStart?.Invoke(this, e);
        }

        /// <summary>Raises the service test passed event.</summary>
        /// <param name="e">Event information to send to registered event handlers.</param>
        protected virtual void OnServiceTestPassed(EventArgs e = null)
        {
            Console.WriteLine($"Service test passed: {DateTime.Now}");
            ServiceTestPassed?.Invoke(this, e);
        }

        /// <summary>Tests service.</summary>
        /// <returns>True if the test passes, false if the test fails.</returns>
        private async Task<bool> TestService()
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
                    return true;
                }

                Console.WriteLine($"Request to {_serviceUrl} failed: {response.StatusCode}!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request to {_serviceUrl} failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>Check service and restart if needed.</summary>
        /// <param name="state">The state.</param>
        private async void CheckServiceAndRestartIfNeeded(object state)
        {
            if (TestService().Result == true)
            {
                if (!_monitoringIsActive)
                {
                    Console.WriteLine($"Monitoring is now active for service {_serviceName}");
                    _monitoringIsActive = true;
                }

                OnServiceTestPassed();
                return;
            }

            if (!_monitoringIsActive)
            {
                // raise a waiting on active event
                OnServiceTestWaitingStart();
                return;
            }

            // raise a test failed event
            OnServiceTestFailed();

            bool serviceIsStopped = false;

            ServiceController sc = new ServiceController(_serviceName);

            switch (sc.Status)
            {
                case ServiceControllerStatus.Running:
                    // raise the stopping service event
                    OnStoppingMonitoredService();

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
                    serviceIsStopped = true;
                    break;

                case ServiceControllerStatus.StopPending:
                default:
                    break;
            }

            if (_killProcess && (!serviceIsStopped))
            {
                Process[] processes = Process.GetProcesses();
                foreach (Process proc in processes)
                {
                    if (proc.ProcessName == _processName)
                    {
                        try
                        {
                            Console.WriteLine($"Found process ({_processName}) after STOP, killing...");
                            proc.Kill();
                            serviceIsStopped = true;
                        }
                        catch (Exception killEx)
                        {
                            Console.WriteLine($"Failed to kill process: {_processName} ({killEx.Message}), will try next loop...");
                            return;
                        }

                        break;
                    }
                }
            }

            if (!serviceIsStopped)
            {
                Console.WriteLine($"Cannot start {_serviceName} while old process is alive! will check next loop...");
                return;
            }

            // raise the stopped service event
            OnStoppedMonitoredService();

            // refresh our service controller to ensure we can actually start the service
            sc.Refresh();

            try
            {
                // raise the starting service event
                OnStartingMonitoredService();

                // ask the service controller to start the service
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

            // TODO(ginoc): Add email notifier
            //if ((!string.IsNullOrEmpty(Program.Configuration["Email:SMTP_Server"])) &&
            //    (!string.IsNullOrEmpty(Program.Configuration["Email:From"])) &&
            //    (!string.IsNullOrEmpty(Program.Configuration["Email:To"])) &&
            //    (!string.IsNullOrEmpty(Program.Configuration["Email:SMTP_User"])) &&
            //    (!string.IsNullOrEmpty(Program.Configuration["Email:SMTP_Password"])))
            //{

            //}

            if (int.TryParse(Program.Configuration["ServiceStopDelaySeconds"], out int delaySeconds))
            {
                _serviceStopDelayMs = delaySeconds * 1000;
            }
            else
            {
                _serviceStopDelayMs = 10 * 1000;
            }

            if ((!string.IsNullOrEmpty(_processName)) &&
                bool.TryParse(Program.Configuration["KillProcess"], out bool killProcess))
            {
                _killProcess = killProcess;
            }
            else
            {
                _killProcess = false;
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
