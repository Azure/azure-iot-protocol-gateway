// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Extensions
{
    using System;
    using System.Threading.Tasks;

    public static class TaskExtensions
    {
        public static void OnFault(this Task task, Action<Task> faultAction)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    faultAction(task);
                }
                return;
            }

            task.ContinueWith(faultAction, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        public static void OnFault(this Task task, Action<Task, object> faultAction, object state)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    faultAction(task, state);
                }
                return;
            }

            task.ContinueWith(faultAction, state, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}