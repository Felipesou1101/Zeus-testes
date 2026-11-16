using System;
using System.Collections.Generic;
using System.Text;

namespace ZeusFiscalMonitor.Domain.ValueObjects
{
    public record ChaveAcesso : ValueObject
    {
        public string chave { get; init; }

        public ChaveAcesso(string chave)
        {
            if (string.IsNullOrWhiteSpace(chave))
            {
                throw new ArgumentException("Chave de acesso não pode ser vazia.", nameof(chave));
            }

            this.chave = chave;
        }

        public override string ToString() => chave;
        public static implicit operator string(ChaveAcesso chaveAcesso) => chaveAcesso.chave;
    }
}
