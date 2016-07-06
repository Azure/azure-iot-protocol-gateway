// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using System.Collections.Generic;
    using System.Linq;

    static class EnumerableExtensions
    {
        public static IEnumerable<List<T>> InSetsOf<T>(this IEnumerable<T> source, int maxSize)
        {
            var result = new List<T>(maxSize);
            foreach (T item in source)
            {
                result.Add(item);
                if (result.Count == maxSize)
                {
                    yield return result;
                    result = new List<T>(maxSize);
                }
            }
            if (result.Any())
            {
                yield return result;
            }
        }
    }
}