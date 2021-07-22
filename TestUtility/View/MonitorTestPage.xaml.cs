using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TestUtility.ViewModel;

namespace TestUtility.View
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MonitorTestPage : Page
    {
        private MonitorTestModel _context;

        /// <summary>The timer.</summary>
        private Timer _timer;

        /// <summary>
        /// Initializes a new instance of the TestUtility.View.MonitorTestPage class.
        /// </summary>
        public MonitorTestPage()
        {
            this.Initialized += MonitorTestPage_Initialized;
            this.Unloaded += MonitorTestPage_Unloaded;

            InitializeComponent();

            _context = new MonitorTestModel();
            DataContext = _context;

            ButtonStartWebHost.Click += ButtonStartWebHost_Click;
            ButtonStopWebHost.Click += ButtonStopWebHost_Click;

            ButtonRespond200.Click += ButtonRespond200_Click;
            ButtonRespond500.Click += ButtonRespond500_Click;
            ButtonFailTimeout.Click += ButtonFailTimeout_Click;
        }

        private void ButtonFailTimeout_Click(object sender, RoutedEventArgs e)
        {
            _context.StatusCode = -1;
            TestWebHost.StatusCode = -1;
            TestWebHost.ResponseBody = "Timeout";
        }

        private void ButtonRespond500_Click(object sender, RoutedEventArgs e)
        {
            _context.StatusCode = 500;
            TestWebHost.StatusCode = 500;
            TestWebHost.ResponseBody = "Error";
        }

        private void ButtonRespond200_Click(object sender, RoutedEventArgs e)
        {
            _context.StatusCode = 200;
            TestWebHost.StatusCode = 200;
            TestWebHost.ResponseBody = "Ok.";
        }

        /// <summary>Event handler. Called by ButtonStopWebHost for click events.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Routed event information.</param>
        private void ButtonStopWebHost_Click(object sender, RoutedEventArgs e)
        {
            if (_context.WebHostIsRunning)
            {
                TestWebHost.StopWebHost();
                _context.WebHostIsRunning = false;
            }
        }

        /// <summary>Event handler. Called by ButtonStartWebHost for click events.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Routed event information.</param>
        private void ButtonStartWebHost_Click(object sender, RoutedEventArgs e)
        {
            if (!_context.WebHostIsRunning)
            {
                TestWebHost.StartWebHost();
                _context.WebHostIsRunning = true;
            }
        }

        /// <summary>Event handler. Called by MonitorTestPage for unloaded events.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Routed event information.</param>
        private void MonitorTestPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
        }

        /// <summary>Event handler. Called by MonitorTestPage for initialized events.</summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">     Event information.</param>
        private void MonitorTestPage_Initialized(object sender, EventArgs e)
        {
            _timer = new Timer(
                ReadServiceStatus,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1));
        }

        /// <summary>Reads service status.</summary>
        /// <param name="state">The state.</param>
        private void ReadServiceStatus(object state)
        {
            ServiceController sc = new ServiceController(_context.ServiceName);

            _context.ServiceStatus = sc.Status.ToString();
        }
    }
}
