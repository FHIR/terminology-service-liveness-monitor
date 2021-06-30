using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace terminology_service_liveness_monitor.Notification
{
    /// <summary>A notification hub.</summary>
    public class NotificationHub
    {
        /// <summary>The current.</summary>
        private static NotificationHub _current;

        /// <summary>
        /// Initializes static members of the
        /// terminology_service_liveness_monitor.Notification.NotificationHub class.
        /// </summary>
        static NotificationHub()
        {
            _current = new NotificationHub();
        }

        /// <summary>
        /// Prevents a default instance of the
        /// terminology_service_liveness_monitor.Notification.NotificationHub class from being
        /// created.
        /// </summary>
        private NotificationHub()
        {
        }

        /// <summary>Gets the current.</summary>
        public static NotificationHub Current => _current;

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
        /// <param name="serviceName">Name of the service.</param>
        public static void OnStoppingMonitoredService(string serviceName)
        {
            Console.WriteLine($"Stopping service {serviceName}...");
            _current.StoppingMonitoredService?.Invoke(_current, null);
        }

        /// <summary>Raises the stopped monitored service event.</summary>
        /// <param name="serviceName">Name of the service.</param>
        public static void OnStoppedMonitoredService(string serviceName)
        {
            _current.StoppedMonitoredService?.Invoke(_current, null);
        }

        /// <summary>Raises the starting monitored service event.</summary>
        public static void OnStartingMonitoredService(string serviceName)
        {
            Console.WriteLine($"Starting service {serviceName}...");
            _current.StartingMonitoredService?.Invoke(_current, null);
        }

        /// <summary>Raises the started monitored service event.</summary>
        /// <param name="serviceName">Name of the service.</param>
        public static void OnStartedMonitoredService(string serviceName)
        {
            Console.WriteLine($"Monitoring is now active for service {serviceName}");
            _current.StartedMonitoredService?.Invoke(_current, null);
        }

        /// <summary>Raises the service test failed event.</summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="serviceUrl"> URL of the service.</param>
        public static void OnServiceTestFailed(string serviceName, string serviceUrl)
        {
            _current.ServiceTestFailed?.Invoke(_current, null);
        }

        /// <summary>Raises the service test waiting event.</summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="serviceUrl"> URL of the service.</param>
        public static void OnServiceTestWaitingStart(string serviceName, string serviceUrl)
        {
            Console.WriteLine($"Waiting for initial success to begin monitoring, service: {serviceName}, url: {serviceUrl}");
            _current.ServiceTestWaitingStart?.Invoke(_current, null);
        }

        /// <summary>Raises the service test passed event.</summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="serviceUrl"> URL of the service.</param>
        public static void OnServiceTestPassed(string serviceName, string serviceUrl)
        {
            Console.WriteLine($"Service test passed: {DateTime.Now}");
            _current.ServiceTestPassed?.Invoke(_current, null);
        }
    }
}
