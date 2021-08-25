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
using terminology_service_liveness_monitor.Notification;

namespace terminology_service_liveness_monitor
{
    /// <summary>A .Net service for monitoring Windows services.</summary>
    public class HostedMonitoringService : IHostedService, IDisposable
    {
        /// <summary>The callback timer.</summary>
        private Timer _timer;

        /// <summary>The polling in seconds.</summary>
        private int _pollingSeconds;

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

        /// <summary>The accept header.</summary>
        private static string _acceptHeader;

        /// <summary>The HTTP timeout in seconds.</summary>
        private int _httpTimeoutSeconds;

        /// <summary>The failures to restart.</summary>
        private int _failuresUntilRestart;

        private int _currentFailures;

        /// <summary>The state.</summary>
        private static MonitoringState _state;

        /// <summary>Values that represent monitoring states.</summary>
        private enum MonitoringState
        {
            Initializing,
            Ok,
            RequestStop,
            WaitingForServiceToStop,
            RequestStart,
            WaitingForFirstSuccess,
        };

        /// <summary>
        /// Initializes a new instance of the terminiology-service-liveness-monitor.HostedMonitoringService class.
        /// </summary>
        public HostedMonitoringService()
        {
            _disposedValue = false;
            _state = MonitoringState.Initializing;
        }

        /// <summary>Tests service.</summary>
        /// <returns>True if the test passes, false if the test fails.</returns>
        private async Task<HttpResponseMessage> TestService()
        {
            HttpClient client = new HttpClient();

            client.Timeout = TimeSpan.FromSeconds(_httpTimeoutSeconds);

            try
            {
                HttpRequestMessage httpRequest = new HttpRequestMessage(
                        HttpMethod.Get,
                        _serviceUrl);

                httpRequest.Headers.Add("Accept", _acceptHeader);

                HttpResponseMessage response = await client.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Request to {_serviceUrl} passed: {response.StatusCode}!");
                }
                else
                {
                    Console.WriteLine($"Request to {_serviceUrl} failed: {response.StatusCode}!");
                }

                return response;


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request to {_serviceUrl} failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>Query if this object is service stopped.</summary>
        /// <returns>True if service stopped, false if not.</returns>
        private bool IsServiceStopped()
        {
            ServiceController sc = new ServiceController(_serviceName);

            return sc.Status == ServiceControllerStatus.Stopped;
        }

        /// <summary>Stops monitored service.</summary>
        /// <returns>An asynchronous result.</returns>
        private async Task StopMonitoredService()
        {
            ServiceController sc = new ServiceController(_serviceName);

            // raise the stopping service event
            NotificationHub.OnStoppingService(_serviceName);

            try
            {
                sc.Stop();
                await Task.Delay(_serviceStopDelayMs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to STOP service {_serviceName}: {ex.Message}");
            }

            sc.Refresh();

            if (_killProcess && (sc.Status != ServiceControllerStatus.Stopped))
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
        }

        /// <summary>Starts monitored service.</summary>
        /// <returns>An asynchronous result.</returns>
        private void StartMonitoredService()
        {
            try
            {
                _currentFailures = 0;

                // raise the starting service event
                NotificationHub.OnStartingService(_serviceName);

                ServiceController sc = new ServiceController(_serviceName);

                // ask the service controller to start the service
                sc.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to START service {_serviceName}: {ex.Message}");
            }
        }

        /// <summary>Process the initializing state. Moves to WaitingForFirstSuccess.</summary>
        /// <returns>The next MonitoringState.</returns>
        private MonitoringState ProcessStateInitializing()
        {
            _currentFailures = 0;

            NotificationHub.OnMonitorInitializing();

            // during startup, assume we are waiting on the monitored service
            return MonitoringState.WaitingForFirstSuccess;
        }

        /// <summary>Process the ok state. Moves to Ok or RequestStop.</summary>
        /// <returns>An asynchronous result that yields a MonitoringState.</returns>
        private async Task<MonitoringState> ProcessStateOk()
        {
            Stopwatch timingWatch = Stopwatch.StartNew();

            HttpResponseMessage response = await TestService();

            long testMS = timingWatch.ElapsedMilliseconds;

            if ((response != null) && (response.IsSuccessStatusCode))
            {
                NotificationHub.OnHttpTestPassed(
                    _serviceName,
                    _serviceUrl,
                    (int)response.StatusCode,
                    testMS);

                _currentFailures = 0;

                // continue monitoring
                return MonitoringState.Ok;
            }

            _currentFailures++;

            NotificationHub.OnHttpTestFailed(
                _serviceName, 
                _serviceUrl, 
                response != null ? (int)response.StatusCode : 0,
                testMS,
                _currentFailures,
                _failuresUntilRestart);

            if (_currentFailures < _failuresUntilRestart)
            {
                return MonitoringState.Ok;
            }

            // restart the service
            return MonitoringState.RequestStop;
        }

        /// <summary>Process the waiting for first success sate. Moves to WaitingForFirstSuccess or Ok.</summary>
        /// <returns>An asynchronous result that yields a MonitoringState.</returns>
        private async Task<MonitoringState> ProcessStateWaitingForFirstSuccess()
        {
            _currentFailures = 0;

            Stopwatch timingWatch = Stopwatch.StartNew();

            HttpResponseMessage response = await TestService();

            long testMS = timingWatch.ElapsedMilliseconds;

            if ((response != null) && (response.IsSuccessStatusCode))
            {
                NotificationHub.OnHttpTestPassed(
                    _serviceName,
                    _serviceUrl,
                    (int)response.StatusCode,
                    testMS);

                // move to standard monitoring
                return MonitoringState.Ok;
            }

            // need initial success before reporting a failure
            NotificationHub.OnWaitingForFirstSuccess(_serviceName);
            return MonitoringState.WaitingForFirstSuccess;
        }

        /// <summary>Process the request stop state. Moves to WaitingForServiceToStop.</summary>
        /// <returns>An asynchronous result that yields a MonitoringState.</returns>
        private async Task<MonitoringState> ProcessStateRequestStop()
        {
            // stop the service
            await StopMonitoredService();
            return MonitoringState.WaitingForServiceToStop;
        }

        /// <summary>Process the waiting for service to stop state. Moves to WaitingForServiceToStop or RequestStart.</summary>
        /// <returns>A MonitoringState.</returns>
        private MonitoringState ProcessStateWaitingForServiceToStop()
        {
            if (IsServiceStopped())
            {
                return MonitoringState.RequestStart;
            }

            return MonitoringState.WaitingForServiceToStop;
        }

        /// <summary>Process the request start state. Moves to WaitingForFirstSuccess.</summary>
        /// <returns>A MonitoringState.</returns>
        private MonitoringState ProcessStateRequestStart()
        {
            // start the service
            StartMonitoredService();
            return MonitoringState.WaitingForFirstSuccess;
        }

        /// <summary>Check service processor.</summary>
        /// <param name="state">The state.</param>
        private async void CheckServiceProcessor(object state)
        {
            Console.WriteLine($"CheckServiceProcessor <<< {DateTime.Now} - state: {_state}");

            // default to remaining in the same state
            MonitoringState nextState = _state;

            try
            {

                switch (_state)
                {
                    case MonitoringState.Initializing:
                        nextState = ProcessStateInitializing();
                        break;

                    case MonitoringState.Ok:
                        nextState = await ProcessStateOk();
                        break;

                    case MonitoringState.WaitingForFirstSuccess:
                        nextState = await ProcessStateWaitingForFirstSuccess();
                        break;

                    case MonitoringState.RequestStop:
                        nextState = await ProcessStateRequestStop();
                        break;

                    case MonitoringState.WaitingForServiceToStop:
                        nextState = ProcessStateWaitingForServiceToStop();
                        break;

                    case MonitoringState.RequestStart:
                        nextState = ProcessStateRequestStart();
                        break;

                    default:
                        break;
                }
            }
            finally
            {
                // move to next state - if there is a change, run quickly
                if (nextState != _state)
                {
                    _state = nextState;
                    _timer?.Change(100, Timeout.Infinite);
                }
                else
                {
                    // use normal timing
                    _timer?.Change(_pollingSeconds * 1000, Timeout.Infinite);
                }
            }
        }

        /// <summary>Triggered when the application host is ready to start the service.</summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns>An asynchronous result.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("HostedMonitorService <<< Starting...");

            _currentFailures = 0;
            _serviceName = Program.Configuration["WindowsServiceName"];
            _processName = Program.Configuration["ProcessName"];
            _serviceUrl = Program.Configuration["ServiceTestUrl"];

            _acceptHeader = Program.Configuration["ServiceAcceptHeader"];

            if (string.IsNullOrEmpty(_acceptHeader))
            {
                _acceptHeader = "text/html";
            }

            Console.WriteLine($"HostedMonitorService <<< accept header: {_acceptHeader}");

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

            Console.WriteLine($"HostedMonitorService <<< service delay stop MS: {_serviceStopDelayMs}");

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

            if (!int.TryParse(intervalSecondString, out _pollingSeconds))
            {
                _pollingSeconds = 30;
            }

            while ((_pollingSeconds * 1000) < _serviceStopDelayMs)
            {
                _pollingSeconds += 5;
            }

            Console.WriteLine($"HostedMonitorService <<< poll interval seconds: {_pollingSeconds}");

            if (int.TryParse(Program.Configuration["HttpTimeoutSeconds"], out int httpTimeoutSeconds))
            {
                _httpTimeoutSeconds = httpTimeoutSeconds;
            }
            else
            {
                _httpTimeoutSeconds = 100;
            }

            Console.WriteLine($"HostedMonitorService <<< HTTP Request Timeout seconds: {_httpTimeoutSeconds}");

            if (int.TryParse(Program.Configuration["FailuresUntilRestart"], out int failuresUntilRestart))
            {
                _failuresUntilRestart = failuresUntilRestart;
            }
            else
            {
                _failuresUntilRestart = 1;
            }

            Console.WriteLine($"HostedMonitorService <<< Failures until a restart is issued: {_failuresUntilRestart}");
            

            _timer = new Timer(
                CheckServiceProcessor,
                null,
                _pollingSeconds * 1000,
                Timeout.Infinite);

            return Task.CompletedTask;
        }

        /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
        ///  graceful.</param>
        /// <returns>An asynchronous result.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
            _timer = null;

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
