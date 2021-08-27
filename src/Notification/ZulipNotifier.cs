using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using terminology_service_liveness_monitor.Models;
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

        /// <summary>True to enable, false to disable.</summary>
        private bool _enabled;

        /// <summary>Stream name to post to.</summary>
        private string _streamName;

        /// <summary>Name of the user.</summary>
        private string _userName;

        /// <summary>Identifier for the stream.</summary>
        private int _streamId;

        /// <summary>Identifier for the user.</summary>
        private int _userId;

        /// <summary>The curl command.</summary>
        private string _curlCommand;

        ///// <summary>List of names of the users.</summary>
        //private string[] _userNames;

        ///// <summary>List of identifiers for the users.</summary>
        //private int[] _userIds;

        /// <summary>Message describing the current status.</summary>
        private ulong _lastMessageId;

        /// <summary>The last message ticks.</summary>
        private long _lastMessageTicks;

        /// <summary>Type of the last message.</summary>
        private NotificationMessageType _lastMessageType;

        /// <summary>Cached test information.</summary>
        private List<TestInfo> _cachedTestInfo;

        /// <summary>Values that represent message types.</summary>
        private enum NotificationMessageType
        {
            None,
            Initializing,
            TestFail,
            TestSuccess,
            TestResult,
            Stopping,
            WaitingStop,
            Starting,
            WaitingStart,
        }

        /// <summary>The maximum message frequency in minutes (1 per n minutes), per message type.</summary>
        private static readonly Dictionary<NotificationMessageType, int> _messageMinuteGate = 
            new Dictionary<NotificationMessageType, int>()
        {
            { NotificationMessageType.None, 0 },
            { NotificationMessageType.Initializing, 0 },
            { NotificationMessageType.TestFail, 0 },
            { NotificationMessageType.TestSuccess, 60 },
            { NotificationMessageType.TestResult, 60 },
            { NotificationMessageType.Stopping, 0 },
            { NotificationMessageType.WaitingStop, 1 },
            { NotificationMessageType.Starting, 0 },
            { NotificationMessageType.WaitingStart, 1 },
        };


        /// <summary>
        /// Initializes a new instance of the
        /// terminology_service_liveness_monitor.Notification.ZulipNotifier class.
        /// </summary>
        public ZulipNotifier()
        {
            _hostId = Program.Configuration["HostId"];
            _serviceName = Program.Configuration["WindowsServiceName"];

            _lastMessageId = 0;

            _streamName = Program.Configuration["Zulip:StreamName"];

            string value = Program.Configuration["Zulip:StreamId"];
            if ((!string.IsNullOrEmpty(value)) && (!int.TryParse(value, out _streamId)))
            {
                _streamId = 0;
            }

            _userName = Program.Configuration["Zulip:UserName"];

            value = Program.Configuration["Zulip:UserId"];
            if ((!string.IsNullOrEmpty(value)) && (!int.TryParse(value, out _userId)))
            {
                _userId = 0;
            }

            _curlCommand = Program.Configuration["CurlCommand"];

            if ((!string.IsNullOrEmpty(_streamName)) ||
                (!string.IsNullOrEmpty(_userName)) ||
                (_streamId != 0) ||
                (_userId != 0))
            {
                _enabled = true;
            }
            else
            {
                _enabled = false;
            }

            _cachedTestInfo = new List<TestInfo>();
        }

        /// <summary>Triggered when the application host is ready to start the service.</summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns>An asynchronous result.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_enabled)
            {
                Console.WriteLine("Zulip notifications are disabled!");
                return Task.CompletedTask;
            }

            if (!TryFindZulipRC(AppContext.BaseDirectory, out string zulipRC))
            {
                Console.WriteLine($"No ZulipRC file found - Zulip notifications are disabled!");
                return Task.CompletedTask;
            }

            _zulipThreadTime = DateTime.Now;
            UpdateTopic();

            _lastMessageType = NotificationMessageType.None;

            Console.WriteLine("Zulip notification service started.");

            if (string.IsNullOrEmpty(_curlCommand))
            {
                _zulipClient = new ZulipClient(zulipRC);
            }
            else
            {
                _zulipClient = new ZulipClient(zulipRC, _curlCommand);
            }

            SendNotification(
                NotificationMessageType.Initializing, 
                $"Zulip notification bot starting up.\n" +
                $"Caller: `{Assembly.GetExecutingAssembly().GetName().Name}`," +
                $" version: `{Assembly.GetExecutingAssembly().GetName().Version}`." +
                $" Target: `{AppContext.TargetFrameworkName}`\n" +
                $"OS: `{System.Runtime.InteropServices.RuntimeInformation.OSDescription}`:" +
                $"`{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}`");

            NotificationHub.Current.MonitorInitializing += HandleMonitorInitializing;
            NotificationHub.Current.HttpTestFailed += HandleHttpTestFailed;
            NotificationHub.Current.HttpTestPassed += HandleHttpTestPassed;
            NotificationHub.Current.StoppingService += HandleStoppingService;
            NotificationHub.Current.WaitingForServiceToStop += HandleWaitingForServiceToStop;
            NotificationHub.Current.StartingService += HandleStartingService;
            NotificationHub.Current.WaitingForFirstSuccess += HandleWaitingForFirstSuccess;

            return Task.CompletedTask;
        }

        /// <summary>Handles the waiting for service to start.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleWaitingForFirstSuccess(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.Stopping,
                $"Waiting on first HTTP success from service: {_serviceName} (may take a few minutes)...");
        }

        /// <summary>Handles the starting service.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleStartingService(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.Stopping,
                $"Starting service: {_serviceName} (may take a few minutes)...");
        }

        /// <summary>Handles the waiting for service to stop.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleWaitingForServiceToStop(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.WaitingStop,
                $"Waiting on service ({_serviceName}) to stop.");
        }

        /// <summary>Handles the stopping service.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleStoppingService(object sender, EventArgs e)
        {
            if (_cachedTestInfo.Any())
            {
                SendNotification(
                    NotificationMessageType.Stopping,
                    BuildTextForTestInfo(null, $":stop_sign: Stopping service {_serviceName!}"));

                _cachedTestInfo.Clear();
            }
            else
            {
                SendNotification(
                    NotificationMessageType.Stopping,
                    $":stop_sign: Stopping service ({_serviceName}) due to failuresS!");
            }
        }

        /// <summary>Handles the HTTP test passed.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleHttpTestPassed(object sender, NotificationEventArgs e)
        {
            // bots apparently can't edit stream messages... researching
            //UpdateMessage($"{DateTime.Now}: Service is running.");

            if (ShouldQueueMessage(NotificationMessageType.TestResult))
            {
                _cachedTestInfo.Add(e.NotificationTestInfo);
            }
            else
            {
                SendNotification(
                    NotificationMessageType.TestResult,
                    BuildTextForTestInfo(e.NotificationTestInfo));

                _cachedTestInfo.Clear();
            }
        }

        /// <summary>Handles the HTTP test failed.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleHttpTestFailed(object sender, NotificationEventArgs e)
        {
            if (ShouldQueueMessage(NotificationMessageType.TestResult))
            {
                _cachedTestInfo.Add(e.NotificationTestInfo);
            }
            else
            {
                SendNotification(
                    NotificationMessageType.TestResult,
                    BuildTextForTestInfo(e.NotificationTestInfo));

                _cachedTestInfo.Clear();
            }
        }

        /// <summary>Builds text for test information.</summary>
        /// <param name="info">         The information.</param>
        /// <param name="messagePrefix">(Optional) The message prefix.</param>
        /// <returns>A string.</returns>
        private string BuildTextForTestInfo(TestInfo info, string messagePrefix = "")
        {
            StringBuilder builder = new StringBuilder();

            bool useSpoiler = _cachedTestInfo.Any();

            if (string.IsNullOrEmpty(messagePrefix))
            {
                builder.AppendLine("HTTP Test Results:");
            }
            else
            {
                builder.AppendLine(messagePrefix);
            }

            if (useSpoiler)
            {
                builder.AppendLine("```spoiler");
            }

            builder.Append("> ");
            builder.Append("| Time ");
            builder.Append("| HTTP");
            builder.Append("| ms");
            builder.Append("| Fail");
            builder.Append("| Mem (MB)");
            builder.Append("| Handles");
            builder.Append("| Threads");
            builder.Append("| #Conn");
            builder.Append("|");
            builder.AppendLine();

            builder.Append("> ");
            builder.Append("|---");
            builder.Append("|---");
            builder.Append("|---");
            builder.Append("|---");
            builder.Append("|---");
            builder.Append("|---");
            builder.Append("|---");
            builder.Append("|---");
            builder.Append("|");
            builder.AppendLine();

            if (info != null)
            {
                builder.Append("> ");
                builder.Append($"| {info.TestDateTime.ToLongTimeString()} ");

                if (info.HttpException != null)
                {
                    if (info.HttpException.InnerException != null)
                    {
                        builder.Append($"|{info.HttpException.InnerException.Message} ");
                    }
                    else
                    {
                        builder.Append($"|{info.HttpException.Message} ");
                    }
                }
                else if (info.HttpStatusCode != null)
                {
                    builder.Append($"| {(int)info.HttpStatusCode}:{info.HttpStatusCode} ");
                }
                else
                {
                    builder.Append("| ");
                }

                builder.Append($"| {info.TestTimeInMS} ");
                builder.Append($"| {info.FailureNumber}/{info.MaxFailureCount} ");
                builder.Append($"| {((double)info.WorkingSet / (1024.0 * 1024.0)).ToString("F3")} ");
                builder.Append($"| {info.HandleCount} ");
                builder.Append($"| {info.ThreadCount} ");
                builder.Append(info.TcpStatsV4 == null ? "| ": $"| {info.TcpStatsV4.CurrentConnections}");
                builder.Append("|");
                builder.AppendLine();
            }

            if (_cachedTestInfo.Any())
            {
                foreach (TestInfo cached in _cachedTestInfo.OrderByDescending(ti => ti.TestDateTime))
                {
                    if (cached == null)
                    {
                        continue;
                    }

                    builder.Append("> ");
                    builder.Append($"| {cached.TestDateTime.ToLongTimeString()} ");

                    if (cached.HttpException != null)
                    {
                        if (cached.HttpException.InnerException != null)
                        {
                            builder.Append($"|{cached.HttpException.InnerException.Message} ");
                        }
                        else
                        {
                            builder.Append($"|{cached.HttpException.Message} ");
                        }
                    }
                    else if (cached.HttpStatusCode != null)
                    {
                        builder.Append($"| {(int)cached.HttpStatusCode}:{cached.HttpStatusCode} ");
                    }
                    else
                    {
                        builder.Append("| ");
                    }

                    builder.Append($"| {cached.TestTimeInMS} ");
                    builder.Append($"| {cached.FailureNumber}/{cached.MaxFailureCount} ");
                    builder.Append($"| {((double)cached.WorkingSet / (1024.0 * 1024.0)).ToString("F3")} ");
                    builder.Append($"| {cached.HandleCount} ");
                    builder.Append($"| {cached.ThreadCount} ");
                    builder.Append(cached.TcpStatsV4 == null? "| " : $"| {cached.TcpStatsV4.CurrentConnections}");
                    builder.Append("|");
                    builder.AppendLine();
                }
            }

            if (useSpoiler)
            {
                builder.AppendLine("```");
            }

            return builder.ToString();
        }

        /// <summary>Handles the monitor initializing.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void HandleMonitorInitializing(object sender, EventArgs e)
        {
            SendNotification(
                NotificationMessageType.Initializing,
                "Monitoring Service initializing...");
        }

        /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
        ///  graceful.</param>
        /// <returns>An asynchronous result.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_zulipClient != null)
            {
                NotificationHub.Current.MonitorInitializing -= HandleMonitorInitializing;
                NotificationHub.Current.HttpTestFailed -= HandleHttpTestFailed;
                NotificationHub.Current.HttpTestPassed -= HandleHttpTestPassed;
                NotificationHub.Current.StoppingService -= HandleStoppingService;
                NotificationHub.Current.WaitingForServiceToStop -= HandleWaitingForServiceToStop;
                NotificationHub.Current.StartingService -= HandleStartingService;
                NotificationHub.Current.WaitingForFirstSuccess -= HandleWaitingForFirstSuccess;
            }

            return Task.CompletedTask;
        }

        /// <summary>Determine if we should queue message.</summary>
        /// <param name="messageType">Type of the message.</param>
        /// <returns>True if it succeeds, false if it fails.</returns>
        private bool ShouldQueueMessage(NotificationMessageType messageType)
        {
            if ((messageType == _lastMessageType) &&
                (_messageMinuteGate[messageType] != 0))
            {
                int minutes = (int)((DateTime.Now.Ticks - _lastMessageTicks) / TimeSpan.TicksPerMinute);
                if (minutes < _messageMinuteGate[messageType])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Sends a notification.</summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="content">    The content.</param>
        private async void SendNotification(
            NotificationMessageType messageType,
            string content)
        {
            if ((messageType == _lastMessageType) && 
                (_messageMinuteGate[messageType] != 0))
            {
                int minutes = (int)((DateTime.Now.Ticks - _lastMessageTicks) / TimeSpan.TicksPerMinute);
                if (minutes < _messageMinuteGate[messageType])
                {
                    return;
                }
            }

            try
            {
                _lastMessageType = messageType;
                _lastMessageTicks = DateTime.Now.Ticks;

                if (!string.IsNullOrEmpty(_streamName))
                {
                    _lastMessageId = await _zulipClient.Messages.SendStream(content, _currentTopic, _streamName);
                }

                if (!string.IsNullOrEmpty(_userName))
                {
                    _lastMessageId = await _zulipClient.Messages.SendPrivate(content, _userName);
                }

                if (_streamId != 0)
                {
                    _lastMessageId = await _zulipClient.Messages.SendStream(content, _currentTopic, _streamId);
                }

                if (_userId != 0)
                {
                    _lastMessageId = await _zulipClient.Messages.SendPrivate(content, _userId);
                }

                //ulong messageId = await _zulipClient.Messages.SendStream(content, _currentTopic, _streamName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ZulipNotifier <<< TrySendStream failed: {ex.Message}");
                Console.WriteLine(ex.InnerException);
                _lastMessageId = 0;
            }
        }

        /// <summary>Updates the message described by content.</summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="content">    The content.</param>
        private async void UpdateMessage(NotificationMessageType messageType, string content)
        {
            if (_lastMessageId == 0)
            {
                SendNotification(messageType, content);
                return;
            }

            var result = await _zulipClient.Messages.TryEdit(_lastMessageId, content);

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
