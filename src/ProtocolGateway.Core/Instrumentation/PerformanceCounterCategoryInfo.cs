// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    using System.Diagnostics;

    public sealed class PerformanceCounterCategoryInfo
    {
        public PerformanceCounterCategoryInfo(string categoryName, PerformanceCounterCategoryType categoryType, string categoryHelp)
        {
            this.CategoryName = categoryName;
            this.CategoryType = categoryType;
            this.CategoryHelp = categoryHelp;
        }

        public string CategoryName { get; private set; }

        public PerformanceCounterCategoryType CategoryType { get; private set; }

        public string CategoryHelp { get; private set; }
    }
}