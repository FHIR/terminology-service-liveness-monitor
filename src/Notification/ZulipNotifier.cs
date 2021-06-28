using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using zulip_cs_lib;

namespace terminology_service_liveness_monitor.Notification
{
    /// <summary>A zulip notifier.</summary>
    public class ZulipNotifier : IHostedService, IDisposable
    {
        /// <summary>True to disposed value.</summary>
        private bool _disposedValue;

        /// <summary>The zulip client.</summary>
        private ZulipClient _zulipClient;

        /// <summary>The notifier creation time.</summary>
        private DateTime _notifierStartTime;

        /// <summary>The most recent start time.</summary>
        private DateTime _recentStartTime;

        /// <summary>The current topic.</summary>
        private string _currentTopic;

        /// <summary>Identifier for the host.</summary>
        private string _hostId;

        /// <summary>Name of the service.</summary>
        private string _serviceName;

        /// <summary>List of names of the streams.</summary>
        private string[] _streamNames;

        /// <summary>List of identifiers for the streams.</summary>
        private int[] _streamIds;

        /// <summary>List of names of the users.</summary>
        private string[] _userNames;

        /// <summary>List of identifiers for the users.</summary>
        private int[] _userIds;

        /// <summary>The current messages.</summary>
        private List<ulong> _currentMessages;

        /// <summary>
        /// Initializes a new instance of the
        /// terminology_service_liveness_monitor.Notification.ZulipNotifier class.
        /// </summary>
        public ZulipNotifier()
        {
            _hostId = Program.Configuration["HostId"];
            _serviceName = Program.Configuration["WindowsServiceName"];

            _streamNames = new string[0];
            _streamIds = new int[0];
            _userNames = new string[0];
            _userIds = new int[0];

            _currentMessages = new List<ulong>();

            string configValue = Program.Configuration["Zulip:Streams"];
            if (!string.IsNullOrEmpty(configValue))
            {
                string[] splitValue = configValue.Split(',');

                List<string> strings = new List<string>();
                List<int> ints = new List<int>();

                foreach (string stringValue in splitValue)
                {
                    if (int.TryParse(stringValue, out int intValue))
                    {
                        ints.Add(intValue);
                    }
                    else
                    {
                        strings.Add(stringValue);
                    }
                }

                if (ints.Count != 0)
                {
                    _streamIds = ints.ToArray();
                }

                if (strings.Count != 0)
                {
                    _streamNames = strings.ToArray();
                }
            }

            configValue = Program.Configuration["Zulip:Users"];
            if (!string.IsNullOrEmpty(configValue))
            {
                string[] splitValue = configValue.Split(',');

                List<string> strings = new List<string>();
                List<int> ints = new List<int>();

                foreach (string stringValue in splitValue)
                {
                    if (int.TryParse(stringValue, out int intValue))
                    {
                        ints.Add(intValue);
                    }
                    else
                    {
                        strings.Add(stringValue);
                    }
                }

                if (ints.Count != 0)
                {
                    _userIds = ints.ToArray();
                }

                if (strings.Count != 0)
                {
                    _userNames = strings.ToArray();
                }
            }
        }

        /// <summary>Triggered when the application host is ready to start the service.</summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns>An asynchronous result.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!TryFindZulipRC(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), out string zulipRC))
            {
                Console.WriteLine($"No ZulipRC file found - Zulip notifications are disabled!");
                return Task.CompletedTask;
            }

            _notifierStartTime = DateTime.Now;
            _recentStartTime = DateTime.Now;
            UpdateTopic();

            Console.WriteLine("Zulip notification service started.");
            _zulipClient = new ZulipClient(zulipRC);

            NotificationHub.Current.ServiceTestFailed += HandleTestFailed;
            NotificationHub.Current.ServiceTestPassed += HandleTestPassed;
            NotificationHub.Current.ServiceTestWaitingStart += HandleWaitingStart;
            NotificationHub.Current.StartedMonitoredService += HandleStartedMonitoredService;
            NotificationHub.Current.StartingMonitoredService += HandleStartingMonitoredService;
            NotificationHub.Current.StoppedMonitoredService += HandleStoppedMonitoredService;
            NotificationHub.Current.StoppingMonitoredService += HandleStoppingMonitoredService;

            return Task.CompletedTask;
        }

        private void HandleStoppingMonitoredService(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void HandleStoppedMonitoredService(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void HandleStartingMonitoredService(object sender, EventArgs e)
        {
            _recentStartTime = DateTime.Now;
            UpdateTopic();

            SendNotification($"Starting {_serviceName} at {_recentStartTime}...");
        }

        private void HandleStartedMonitoredService(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void HandleWaitingStart(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void HandleTestPassed(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void HandleTestFailed(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
        ///  graceful.</param>
        /// <returns>An asynchronous result.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_zulipClient != null)
            {
                NotificationHub.Current.ServiceTestFailed -= HandleTestFailed;
                NotificationHub.Current.ServiceTestPassed -= HandleTestPassed;
                NotificationHub.Current.ServiceTestWaitingStart -= HandleWaitingStart;
                NotificationHub.Current.StartedMonitoredService -= HandleStartedMonitoredService;
                NotificationHub.Current.StartingMonitoredService -= HandleStartingMonitoredService;
                NotificationHub.Current.StoppedMonitoredService -= HandleStoppedMonitoredService;
                NotificationHub.Current.StoppingMonitoredService -= HandleStoppingMonitoredService;
            }

            return Task.CompletedTask;
        }

        /// <summary>Sends a notification.</summary>
        /// <param name="content">The content.</param>
        private async void SendNotification(string content)
        {
            List<Task<(bool success, string details, ulong messageId)>> tasks = new();

            // note: use Try versions so that we cannot throw here

            if (_streamNames.Length > 0)
            {
                tasks.Add(_zulipClient.Messages.TrySendStream(content, _currentTopic, _streamNames));
            }
            
            if (_streamIds.Length > 0)
            {
                tasks.Add(_zulipClient.Messages.TrySendStream(content, _currentTopic, _streamIds));
            }

            if (_userNames.Length > 0)
            {
                tasks.Add(_zulipClient.Messages.TrySendPrivate(content, _userNames));
            }

            if (_userIds.Length > 0)
            {
                tasks.Add(_zulipClient.Messages.TrySendPrivate(content, _userIds));
            }

            List<ulong> messages = new();

            (bool success, string details, ulong messageId)[] runner = await Task.WhenAll(tasks);

            foreach ((bool success, string details, ulong messageId) result in runner)
            {
                if (result.success)
                {
                    messages.Add(result.messageId);
                }
                else
                {
                    Console.WriteLine($"ZulipNotifier.SendNotification warning! A request failed: {result.details}");
                }
            }

            _currentMessages = messages;
        }

        /// <summary>Updates the topic.</summary>
        /// <returns>A string.</returns>
        private void UpdateTopic()
        {
            _currentTopic = $"{_hostId}: {_serviceName} - {_recentStartTime}";
        }

        /// <summary>Searches for the first zulip RC file.</summary>
        /// <exception cref="DirectoryNotFoundException">Thrown when the requested directory is not
        ///  present.</exception>
        /// <param name="startingDir">The starting dir.</param>
        /// <param name="rcFilename"> [out] Filename of the rectangle file.</param>
        /// <returns>The found zulip rectangle.</returns>
        private static bool TryFindZulipRC(string startingDir, out string rcFilename)
        {
            string currentDir = startingDir;
            string filePath = Path.Combine(currentDir, "zuliprc");

            while (!File.Exists(filePath))
            {
                // check for /secrests/.zuliprc
                string pathInSubdir = Path.Combine(currentDir, "secrets", "zuliprc");

                if (File.Exists(pathInSubdir))
                {
                    rcFilename = pathInSubdir;
                    return true;
                }

                currentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));

                if (currentDir == Path.GetPathRoot(currentDir))
                {
                    rcFilename = null;
                    return false;
                }

                filePath = Path.Combine(currentDir, "zuliprc");
            }

            rcFilename = filePath;
            return true;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the
        /// terminology_service_liveness_monitor.Notification.ZulipNotifier and optionally releases the
        /// managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to
        ///  release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
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
