using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeTestCs
{
    class StockUsList
    {
        private string code;
        private string name;
        private string market;
        

        public StockUsList(string name, string code, string market)
        {
            this.code = code;
            this.name = name;
            this.market = market;
        }

        public string Code { get { return code; } }
        public string Name { get { return name; } }
        public string Market { get { return market; } }
    }
}
