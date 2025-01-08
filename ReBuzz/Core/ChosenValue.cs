using System;
using System.Diagnostics.CodeAnalysis;

namespace ReBuzz.Core
{
    public readonly record struct ChosenValue<T>
    {
        private readonly T value;
        public static ChosenValue<T> Nothing => new(false, default);
        public static ChosenValue<T> Just([MaybeNull] T value) => new(true, value ?? throw new ArgumentNullException(nameof(value)));
        
        public bool HasValue { get; }
        public T Value() => HasValue ? value : throw new InvalidOperationException("No value");

        private ChosenValue(bool hasValue, [MaybeNull] T value)
        {
            HasValue = hasValue;
            this.value = value;
        }
    }
}