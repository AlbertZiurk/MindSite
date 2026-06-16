using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MindSite.Entities
{
    public class ServicoAdicional
    {
        public int Id { get; set; }
        public int ServicoId { get; set; }
        public string DescricaoMudanca { get; set; }
        public decimal ValorAdicional { get; set; } // Valor desta alteração específica
        public bool PagamentoInicialFeito { get; set; }
        public bool PagamentoFinalFeito { get; set; }
        public bool StatusAprovado { get; set; } // Definido quando o fornecedor aceita a mudança por um preço
    }
}