// <copyright file="NotificationEventArgs.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using terminology_service_liveness_monitor.Models;

namespace terminology_service_liveness_monitor.Notification
{
    /// <summary>Additional information for notification events.</summary>
    public class NotificationEventArgs : EventArgs
    {
        /// <summary>Gets or sets information describing the current test.</summary>
        public TestInfo NotificationTestInfo { get; set; }
    }
}
