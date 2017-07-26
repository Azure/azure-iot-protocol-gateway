// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    using Microsoft.Azure.Devices.Common.Cloud;
    using Microsoft.Azure.Devices.Tracing;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    public class WindowsPerformanceCounterManager : IPerformanceCounterManager
    {
        readonly Dictionary<Tuple<string, string>, SafePerformanceCounter> counterMap;

        protected WindowsPerformanceCounterManager(IDictionary<PerformanceCounterCategoryInfo, CounterCreationData[]> counterDefinitions)
        {
            this.counterMap = new Dictionary<Tuple<string, string>, SafePerformanceCounter>();

            foreach (KeyValuePair<PerformanceCounterCategoryInfo, CounterCreationData[]> counterCategoryInfo in counterDefinitions)
            {
                PerformanceCounterCategory category = GetOrCreateCounterCategory(counterCategoryInfo.Key, counterCategoryInfo.Value);

                PerformanceCounter[] counters = category.GetCounters();
                foreach (PerformanceCounter counter in counters)
                {
                    counter.ReadOnly = false;
                    this.counterMap.Add(Tuple.Create(category.CategoryName, counter.CounterName), new SafePerformanceCounter(counter));
                }
            }
        }

        static PerformanceCounterCategory GetOrCreateCounterCategory(PerformanceCounterCategoryInfo categoryInfo, CounterCreationData[] counters)
        {
            bool creationPending = true;
            bool categoryExists = false;
            string categoryName = categoryInfo.CategoryName;
            var counterNames = new HashSet<string>(counters.Select(info => info.CounterName));
            PerformanceCounterCategory category = null;
            if (PerformanceCounterCategory.Exists(categoryName))
            {
                RegistrationServiceGatewayEventSource.Log.Informational(
                    "WindowsPerformanceCounterExists",
                    "MqttGateway",
                    null,
                    null,
                    "Category: {0}. Counters: {1}".FormatInvariant(categoryName, string.Concat(counters.Select(info => info.CounterName))),
                    null,
                    null,
                    null,
                    0,
                    0);
                categoryExists = true;
                category = new PerformanceCounterCategory(categoryName);
                PerformanceCounter[] counterList = category.GetCounters();
                if (category.CategoryType == categoryInfo.CategoryType && counterList.Length == counterNames.Count)
                {
                    creationPending = counterList.Any(x => !counterNames.Contains(x.CounterName));
                }
            }

            if (creationPending)
            {
                if (categoryExists)
                {
                    PerformanceCounterCategory.Delete(categoryName);
                }
                var counterCollection = new CounterCreationDataCollection(counters);

                category = PerformanceCounterCategory.Create(
                    categoryInfo.CategoryName,
                    categoryInfo.CategoryHelp,
                    categoryInfo.CategoryType,
                    counterCollection);
                RegistrationServiceGatewayEventSource.Log.Informational(
                    "WindowsPerformanceCounterCreated",
                    "MqttGateway",
                    null,
                    null,
                    "Category: {0}. Counters: {1}".FormatInvariant(categoryName, string.Concat(counters.Select(info => info.CounterName))),
                    null,
                    null,
                    null,
                    0,
                    0);
            }
            return category;
        }

        public IPerformanceCounter GetCounter(string category, string name)
        {
            SafePerformanceCounter counter;
            if (!this.counterMap.TryGetValue(Tuple.Create(category, name), out counter))
            {

                RegistrationServiceGatewayEventSource.Log.Informational(
                    "WindowsPerformanceCounterNotFound",
                    "MqttGateway",
                    null,
                    null,
                    "Category: {0}. Counter: {1}".FormatInvariant(category, name),
                    null,
                    null,
                    null,
                    0,
                    0);

                throw new InvalidOperationException(string.Format("Counter named `{0}` could not be found under category `{1}`", name, category));
            }

            return counter;
        }
    }
}
