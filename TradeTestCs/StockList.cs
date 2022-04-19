using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeTestCs
{
    class StockList
    {
        private string code;
        private string name;
        private uint totalValue; 

        public StockList(string name, string code, uint totalValue)
        {
            this.code = code;
            this.name = name;
            this.totalValue = totalValue;
        }

        public string Code { get { return code; } }
        public string Name { get { return name; } }
        public uint Value { get { return totalValue; } }
    }
}
