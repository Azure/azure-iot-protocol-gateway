// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Tests.Extensions
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class TaskExtensions
    {
        public static async Task WhenSome(IEnumerable<Task> tasks, int count)
        {
            List<Task> remainingTasks = tasks.ToList();
            int completed = 0;
            while (completed < count)
            {
                Task completedTask = await Task.WhenAny(remainingTasks);
                completedTask.Wait(0);
                remainingTasks.Remove(completedTask);
                completed++;
            }
        }
    }
}