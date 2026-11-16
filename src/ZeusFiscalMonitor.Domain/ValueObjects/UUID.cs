using System;
using System.Collections.Generic;
using System.Text;

namespace ZeusFiscalMonitor.Domain.ValueObjects
{
    public record UUID : ValueObject
    {
        public Guid Value { get; init; }

        public UUID(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("UUID não pode ser vazio.", nameof(value));
            }

            Value = value;
        }

        public static implicit operator Guid(UUID uuid) => uuid.Value;
        public override string ToString() => Value.ToString();
    }
}
