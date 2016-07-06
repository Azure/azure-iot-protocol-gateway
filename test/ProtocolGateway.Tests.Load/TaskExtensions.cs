// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using System;
    using System.Threading.Tasks;

    static class TaskExtensions
    {
        public static void LogOnFaulure(this Task task)
        {
            task.ContinueWith(
                t =>
                {
                    // todo: log
                    Console.WriteLine(t.Exception);
                },
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}