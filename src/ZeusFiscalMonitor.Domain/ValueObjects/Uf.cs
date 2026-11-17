using System;
using System.Collections.Generic;
using System.Text;

namespace ZeusFiscalMonitor.Domain.ValueObjects
{
    public sealed record UF
    {
        private static readonly HashSet<string> _ufs = new HashSet<string>
        {
            "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO",
            "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI",
            "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
        };

        public string Sigla { get; }

        public UF(string sigla)
        {
            if (string.IsNullOrWhiteSpace(sigla))
            {
                throw new ArgumentException("Sigla de UF não pode ser nula ou vazia.", nameof(sigla));
            }

            var normalizedSigla = sigla.ToUpperInvariant();

            if (!_ufs.Contains(normalizedSigla))
            {
                throw new ArgumentException($"Sigla de UF inválida: {sigla}");
            }

            Sigla = normalizedSigla;
        }

        public override string ToString() => Sigla;
        public static implicit operator string(UF uf) => uf.Sigla;
    }
}
