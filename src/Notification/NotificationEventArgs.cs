// <copyright file="NotificationEventArgs.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace terminology_service_liveness_monitor.Notification
{
    /// <summary>Additional information for notification events.</summary>
    public class NotificationEventArgs : EventArgs
    {
        /// <summary>Gets or sets the name of the service.</summary>
        public string ServiceName { get; set; }

        /// <summary>Gets or sets URL of the test.</summary>
        public string TestUrl { get; set; }

        /// <summary>Gets or sets the HTTP status code.</summary>
        public int HttpStatusCode { get; set; }

        /// <summary>Gets or sets the test time in milliseconds.</summary>
        public long TestTimeInMS { get; set; }

        /// <summary>Gets or sets the failure number.</summary>
        public int FailureNumber { get; set; }

        /// <summary>Gets or sets the number of maximum failures.</summary>
        public int MaxFailureCount { get; set; }
    }
}
