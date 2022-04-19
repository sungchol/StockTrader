using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeTestCs
{
    //번호";
    //코드번호";
    //주식명";
    //보유수량";
    //매입금액";
    //매입단가";
    //현재가액";
    //평가금액";
    //평가손익";
    //"손익율";
    //"실현손익";
    //"정산손익";
    class HoldingStock
    {
        //주식명, 코드, 수량, 단가, 매입금액, 현재가, 평가액
        public int orderNum;
        public string name;
        public string code;
        public int amount;
        public double unitPrice;
        public double buyPrice;
        public double nowPrice;
        public double evaluatePrice;
        public double evaluateProfit;
        public double profitRate;
        public double realizeProfit;
        public double netRealProfit;
        public string market;

        public object[] MakeRow()
        {
            object[] row = new object[12];
            row[0] = this.orderNum;                                //번호";
            row[1] = this.code;                        //코드번호";
            row[2] = this.name;                        //주식명";
            row[3] = Util.Comma(this.amount);          //보유수량";
            row[4] = Util.Comma(Math.Round(this.buyPrice,0));        //매입금액";
            row[5] = Util.Comma(Math.Round(this.unitPrice,1), true);       //매입단가";
            row[6] = Util.Comma(this.nowPrice);        //현재가액";
            row[7] = Util.Comma(Math.Round(this.evaluatePrice));   //평가금액";
            row[8] = Util.Comma(Math.Round(this.evaluateProfit));  //평가손익";
            row[9] = Util.Comma(Math.Round(this.profitRate,2), true);      //손익율;
            row[10] = Util.Comma(Math.Round(this.realizeProfit));                              //실현손익;
            row[11] = Util.Comma(Math.Round(this.netRealProfit));                              //정산손익;

            return row;
        }

        public object[] MakeRowUsStock()
        {
            object[] row = new object[12];
            row[0] = this.orderNum;                                //번호";
            row[1] = this.code;                        //코드번호";
            row[2] = this.name;                        //주식명";
            row[3] = Util.Comma(this.amount);          //보유수량";
            row[4] = Util.Comma(Math.Round(this.buyPrice, 2), true);        //매입금액";
            row[5] = Util.Comma(Math.Round(this.unitPrice, 2), true);       //매입단가";
            row[6] = Util.Comma(this.nowPrice, true);        //현재가액";
            row[7] = Util.Comma(Math.Round(this.evaluatePrice, 2), true);   //평가금액";
            row[8] = Util.Comma(Math.Round(this.evaluateProfit,2), true);  //평가손익";
            row[9] = Util.Comma(Math.Round(this.profitRate, 2), true);      //손익율;
            row[10] = Util.Comma(Math.Round(this.realizeProfit, 2), true);                              //실현손익;
            row[11] = Util.Comma(Math.Round(this.netRealProfit, 2), true);

            return row;
        }
    }
}
