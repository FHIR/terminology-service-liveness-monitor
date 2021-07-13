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

        /// <summary>The most recent start time.</summary>
        private DateTime _zulipThreadTime;

        /// <summary>The current topic.</summary>
        private string _currentTopic;

        /// <summary>Identifier for the host.</summary>
        private string _hostId;

        /// <summary>Name of the service.</summary>
        private string _serviceName;

        /// <summary>Stream name to post to.</summary>
        private string _streamName;

        ///// <summary>List of names of the users.</summary>
        //private string[] _userNames;

        ///// <summary>List of identifiers for the users.</summary>
        //private int[] _userIds;

        /// <summary>Message describing the current status.</summary>
        private ulong _currentMessage;

        /// <summary>Values that represent message types.</summary>
        private enum NotificationMessageType
        {
            None,
            Starting,
            Started,
            Stopping,
            Stopped,
            TestFail,
            TestSuccess,
            WaitingStart,
            Initializing,
        }

        private NotificationMessageType _lastMessageType;

        /// <summary>
        /// Initializes a new instance of the
        /// terminology_service_liveness_monitor.Notification.ZulipNotifier class.
        /// </summary>
        public ZulipNotifier()
        {
            _hostId = Program.Configuration["HostId"];
            _serviceName = Program.Configuration["WindowsServiceName"];

            _currentMessage = 0;

            _streamName = Program.Configuration["Zulip:StreamName"];

            // TODO(ginoc): private messages are currently disabled - KISS
            #if CAKE
            configValue = Program.Configuration["Zulip:PrivateMessageUsers"];
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
            #endif
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

            if (string.IsNullOrEmpty(_streamName))
            {
                Console.WriteLine($"Empty Zulip Stream Name - Zulip notifications are disabled!");
                return Task.CompletedTask;
            }

            _zulipThreadTime = DateTime.Now;
            UpdateTopic();

            _lastMessageType = NotificationMessageType.None;

            Console.WriteLine("Zulip notification service started.");
            _zulipClient = new ZulipClient(zulipRC);

            SendNotification(
                NotificationMessageType.Initializing, 
                $"{DateTime.Now}: Initializing the monitor service, service name: {_serviceName}.");

            if (!string.IsNullOrEmpty(_streamName))
            {
                NotificationHub.Current.ServiceTestFailed += HandleTestFailed;
                //NotificationHub.Current.ServiceTestPassed += HandleTestPassed;
                NotificationHub.Current.ServiceTestWaitingStart += HandleWaitingStart;
                NotificationHub.Current.StartedMonitoredService += HandleStartedMonitoredService;
                NotificationHub.Current.StartingMonitoredService += HandleStartingMonitoredService;
                NotificationHub.Current.StoppedMonitoredService += HandleStoppedMonitoredService;
                NotificationHub.Current.StoppingMonitoredService += HandleStoppingMonitoredService;
            }

            return Task.CompletedTask;
        }

        /// <summary>Handles the stopping monitored service.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleStoppingMonitoredService(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.Stopping,
                $"{DateTime.Now}: Issued stop request, service: {_serviceName}");
        }

        /// <summary>Handles the stopped monitored service.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleStoppedMonitoredService(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.Stopped,
                $"{DateTime.Now}: Service {_serviceName} is stopped.");
        }

        /// <summary>Handles the starting monitored service.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleStartingMonitoredService(object sender, EventArgs e)
        {
            _zulipThreadTime = DateTime.Now;
            UpdateTopic();

            SendNotification(
                NotificationMessageType.Starting,
                $"{DateTime.Now}: Issueed start request, service: {_serviceName}");
        }

        /// <summary>Handles the started monitored service.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleStartedMonitoredService(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.Started,
                $"{DateTime.Now}: Service is up!");
        }

        /// <summary>Handles the waiting start.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleWaitingStart(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.WaitingStart,
                $"{DateTime.Now}: Waiting on service ({_serviceName}) after start...");
        }

        /// <summary>Handles the test passed.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleTestPassed(object sender, EventArgs e)
        {
            // bots apparently can't edit messages... researching
            //UpdateMessage($"{DateTime.Now}: Service is running.");

            SendNotification(
                NotificationMessageType.TestSuccess,
                $"{DateTime.Now}: Service is up!");
        }

        /// <summary>Handles the test failed.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleTestFailed(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.TestFail,
                $"{DateTime.Now}: Service is down! Will restart...");
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
                //NotificationHub.Current.ServiceTestPassed -= HandleTestPassed;
                NotificationHub.Current.ServiceTestWaitingStart -= HandleWaitingStart;
                NotificationHub.Current.StartedMonitoredService -= HandleStartedMonitoredService;
                NotificationHub.Current.StartingMonitoredService -= HandleStartingMonitoredService;
                NotificationHub.Current.StoppedMonitoredService -= HandleStoppedMonitoredService;
                NotificationHub.Current.StoppingMonitoredService -= HandleStoppingMonitoredService;
            }

            return Task.CompletedTask;
        }

        /// <summary>Sends a notification.</summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="content">    The content.</param>
        private async void SendNotification(NotificationMessageType messageType, string content)
        {
            if (messageType == _lastMessageType)
            {
                return;
            }

            var result = await _zulipClient.Messages.TrySendStream(content, _currentTopic, _streamName);

            if (result.success == true)
            {
                _currentMessage = result.messageId;
            }
            else
            {
                Console.WriteLine($"ZulipNotifier <<< TrySendStream failed: {result.details}");
                _currentMessage = 0;
            }

            _lastMessageType = messageType;
        }

        /// <summary>Updates the message described by content.</summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="content">    The content.</param>
        private async void UpdateMessage(NotificationMessageType messageType, string content)
        {
            if (_currentMessage == 0)
            {
                SendNotification(messageType, content);
                return;
            }

            var result = await _zulipClient.Messages.TryEdit(_currentMessage, content);

            if (result.success != true)
            {
                Console.WriteLine($"Update <<< failed! details: {result.details}");

                SendNotification(messageType, content);
                return;
            }
        }

        /// <summary>Updates the topic.</summary>
        /// <returns>A string.</returns>
        private void UpdateTopic()
        {
            _currentTopic = $"{_hostId}: {_serviceName} - {_zulipThreadTime}";
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
