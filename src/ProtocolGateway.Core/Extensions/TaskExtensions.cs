// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Extensions
{
    using System;
    using System.Threading.Tasks;

    public static class TaskExtensions
    {
        public static void OnFault(this Task task, Action<Task> faultAction)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                case TaskStatus.Canceled:
                    break;
                case TaskStatus.Faulted:
                    faultAction(task);
                    break;
                default:
                    task.ContinueWith(faultAction, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                    break;
            }
        }

        public static void OnFault(this Task task, Action<Task, object> faultAction, object state)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                case TaskStatus.Canceled:
                    break;
                case TaskStatus.Faulted:
                    faultAction(task, state);
                    break;
                default:
                    task.ContinueWith(faultAction, state, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                    break;
            }
        }
    }
}