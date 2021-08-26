// <copyright file="TcpStats.cs" company="Microsoft Corporation">
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
    /// <summary>A TCP statistics.</summary>
    public class TcpStats
    {
        /// <summary>Gets or sets the current connections.</summary>
        public long CurrentConnections { get; set; }

        /// <summary>Gets or sets the cumulative connections.</summary>
        public long CumulativeConnections { get; set; }

        /// <summary>Gets or sets the initiated connections.</summary>
        public long InitiatedConnections { get; set; }

        /// <summary>Gets or sets the accepted connection.</summary>
        public long AcceptedConnections { get; set; }

        /// <summary>Gets or sets the failed connections.</summary>
        public long FailedConnections { get; set; }

        /// <summary>Gets or sets the reset conenctions.</summary>
        public long ResetConenctions { get; set; }
    }
}
