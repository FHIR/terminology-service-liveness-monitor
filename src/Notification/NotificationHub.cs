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

        /// <summary>Occurs when the Service Monitor is Initializing.</summary>
        public event EventHandler MonitorInitializing;

        /// <summary>Occurs when Service Test Failed.</summary>
        public event EventHandler HttpTestFailed;

        /// <summary>Occurs when Service Test Passed.</summary>
        public event EventHandler HttpTestPassed;

        /// <summary>Occurs when Stopping Service.</summary>
        public event EventHandler StoppingService;

        /// <summary>Occurs when Waiting For Service To Stop.</summary>
        public event EventHandler WaitingForServiceToStop;

        /// <summary>Occurs when Starting Service.</summary>
        public event EventHandler StartingService;

        /// <summary>Occurs when Waiting For the first Success.</summary>
        public event EventHandler WaitingForFirstSuccess;

        /// <summary>Executes the monitor initializing action.</summary>
        public static void OnMonitorInitializing()
        {
            Console.WriteLine($"NotificationHub <<< {DateTime.Now} - Monitoring Service is Initializing...");
            _current.MonitorInitializing?.Invoke(_current, null);
        }

        /// <summary>Raises the http test failed event.</summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="serviceUrl"> URL of the service.</param>
        public static void OnHttpTestFailed(string serviceName, string serviceUrl)
        {
            Console.WriteLine($"NotificationHub << {DateTime.Now} - Http test FAILED!");
            _current.HttpTestFailed?.Invoke(_current, null);
        }

        /// <summary>Raises the http test passed event.</summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="serviceUrl"> URL of the service.</param>
        public static void OnHttpTestPassed(string serviceName, string serviceUrl)
        {
            Console.WriteLine($"NotificationHub <<< {DateTime.Now} - Http test passed.");
            _current.HttpTestPassed?.Invoke(_current, null);
        }

        /// <summary>Executes the stopping service action.</summary>
        /// <param name="serviceName">Name of the service.</param>
        public static void OnStoppingService(string serviceName)
        {
            Console.WriteLine($"NotificationHub <<< {DateTime.Now} - Stopping service: {serviceName}");
            _current.StoppingService?.Invoke(_current, null);
        }

        /// <summary>Executes the waiting for service to stop action.</summary>
        /// <param name="serviceName">Name of the service.</param>
        public static void OnWaitingForServiceToStop(string serviceName)
        {
            Console.WriteLine($"NotificationHub <<< {DateTime.Now} - Waiting for service to stop: {serviceName}");
            _current.WaitingForServiceToStop?.Invoke(_current, null);
        }

        /// <summary>Executes the starting service action.</summary>
        /// <param name="serviceName">Name of the service.</param>
        public static void OnStartingService(string serviceName)
        {
            Console.WriteLine($"NotificationHub <<< {DateTime.Now} - Starting service: {serviceName}");
            _current.StartingService?.Invoke(_current, null);
        }

        /// <summary>Executes the waiting for service to start action.</summary>
        /// <param name="serviceName">Name of the service.</param>
        public static void OnWaitingForFirstSuccess(string serviceName)
        {
            Console.WriteLine($"NotificationHub <<< {DateTime.Now} - Waiting for first success: {serviceName}");
            _current.WaitingForFirstSuccess?.Invoke(_current, null);
        }


    }
}
