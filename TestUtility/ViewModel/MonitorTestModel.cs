using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TestUtility.ViewModel
{
    public class MonitorTestModel : INotifyPropertyChanged
    {
        /// <summary>Name of the service.</summary>
        private string _serviceName;

        /// <summary>The service status.</summary>
        private string _serviceStatus;

        /// <summary>True if web host is running.</summary>
        private bool _webHostIsRunning;

        /// <summary>The status code.</summary>
        private int _statusCode;

        /// <summary>Gets or sets the name of the service.</summary>
        public string ServiceName
        { 
            get => _serviceName;
            set
            {
                _serviceName = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>Gets or sets the service status.</summary>
        public string ServiceStatus
        {
            get => _serviceStatus;
            set
            {
                _serviceStatus = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>Gets or sets a value indicating whether the web host is running.</summary>
        public bool WebHostIsRunning
        {
            get => _webHostIsRunning;
            set
            {
                _webHostIsRunning = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>Gets or sets the status code.</summary>
        public int StatusCode
        {
            get => _statusCode;
            set
            {
                _statusCode = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>Occurs when a property value changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the TestUtility.ViewModel.MonitorTestModel class.
        /// </summary>
        public MonitorTestModel()
        {
            _serviceName = "W3SVC";
            _serviceStatus = "Unknown...";
            _webHostIsRunning = false;
            _statusCode = 200;
        }

        /// <summary>Notifies a property changed.</summary>
        /// <param name="propertyName">(Optional) Name of the property.</param>
        protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            // grab a copy to handle the case where an event handler is removed during the call (prevent memory leak)
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
