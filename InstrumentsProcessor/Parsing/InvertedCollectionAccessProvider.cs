// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Performance.SDK.Processing;

namespace InstrumentsProcessor.AccessProviders
{
    internal struct InvertedCollectionAccessProvider<TCollectionAccessProvider, TCollection, T>
        : ICollectionAccessProvider<TCollection, T>
        where TCollectionAccessProvider : ICollectionAccessProvider<TCollection, T>
    {
        private readonly TCollectionAccessProvider provider;

        public InvertedCollectionAccessProvider(TCollectionAccessProvider provider)
        {
            this.provider = provider;
        }

        public T PastEndValue
        {
            get
            {
                return this.provider.PastEndValue;
            }
        }

        public bool HasUniqueStart
        {
            get
            {
                return false;
            }
        }

        public bool Equals(TCollection x, TCollection y)
        {
            return x.Equals(y);
        }

        public int GetCount(TCollection collection)
        {
            return this.provider.GetCount(collection);
        }

        public int GetHashCode(TCollection x)
        {
            return x.GetHashCode();
        }

        public TCollection GetParent(TCollection collection)
        {
            throw new NotImplementedException();
        }

        public T GetValue(TCollection collection, int index)
        {
            var length = this.provider.GetCount(collection);
            if (length == 0)
            {
                return this.provider.GetValue(collection, index);
            }

            if (index < length)
            {
                return this.provider.GetValue(collection, length - index - 1);
            }

            return this.PastEndValue;
        }

        public bool IsNull(TCollection value)
        {
            return this.provider.IsNull(value);
        }
    }
}