using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeTestCs
{
    class WatchStock
    {

        public string code;
        public string name;
        public int price0;
        public int price1;
        public float rate;

        public bool isChanged;

        public WatchStock()
        {

            this.code = "";
            this.name = "";
            this.price0 = 0;
            this.price1 = 0;
            this.isChanged = false;
        }

        public WatchStock(string market, string code)
        {

            this.code = code;
            this.name = market;
            this.price0 = 0;
            this.price1 = 0;
            this.isChanged = false;
        }

        public bool Equals(string code)
        {
            return this.code.Equals(code);
        }

        public object[] MakeRowStock()
        {
            object[] row = { this.name, this.code, this.price0, this.price1, this.isChanged };
            return row;
        }
    }
}
