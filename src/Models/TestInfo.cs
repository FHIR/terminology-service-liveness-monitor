// <copyright file="TestInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace terminology_service_liveness_monitor.Models
{
    /// <summary>Information about the test.</summary>
    public class TestInfo
    {
        /// <summary>Gets or sets the test date time.</summary>
        public DateTime TestDateTime { get; set; }

        /// <summary>Gets or sets the name of the service.</summary>
        public string ServiceName { get; set; }

        /// <summary>Gets or sets URL of the test.</summary>
        public string TestUrl { get; set; }

        /// <summary>Gets or sets the HTTP status code.</summary>
        public System.Net.HttpStatusCode? HttpStatusCode { get; set; }

        /// <summary>Gets or sets a value indicating whether this object is success status code.</summary>
        public bool IsSuccessStatusCode { get; set; }

        /// <summary>Gets the procedure information exception.</summary>
        public Exception ProcInfoException { get; set; }

        /// <summary>Gets or sets the net information exception.</summary>
        public Exception NetInfoException { get; set; }

        /// <summary>Gets or sets the HTTP exception.</summary>
        public Exception HttpException { get; set; }

        /// <summary>Gets or sets the test time in milliseconds.</summary>
        public long TestTimeInMS { get; set; }

        /// <summary>Gets or sets the failure number.</summary>
        public int FailureNumber { get; set; }

        /// <summary>Gets or sets the number of maximum failures.</summary>
        public int MaxFailureCount { get; set; }

        /// <summary>Gets or sets the set the working belongs to.</summary>
        public long WorkingSet { get; set; }

        /// <summary>Gets or sets the size of the paged memory.</summary>
        public long PagedMemorySize { get; set; }

        /// <summary>Gets or sets the size of the private memory.</summary>
        public long PrivateMemorySize { get; set; }

        /// <summary>Gets or sets the size of the virtual memory.</summary>
        public long VirtualMemorySize { get; set; }

        /// <summary>Gets or sets the number of handles.</summary>
        public int HandleCount { get; set; }

        /// <summary>Gets or sets the number of handles.</summary>
        public int ThreadCount { get; set; }

        /// <summary>Gets or sets the TCP statistics v 4.</summary>
        public TcpStats TcpStatsV4 { get; set; }

        /// <summary>Gets or sets the TCP statistics v 6.</summary>
        public TcpStats TcpStatsV6 { get; set; }
    }
}
