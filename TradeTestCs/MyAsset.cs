using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeTestCs
{
    class MyAsset
    {
        //국내주식현황
        public long depositKor;
        public double stockBuy;
        public double stockValue;
        public double stockProfit;

        public double depositUsd;
        public double stockBuyUs;
        public double stockValueUs;
        public double stockProfitUs;

        public bool isUpdated = false;

        public void CalulateAsset(List<HoldingStock> kor_stocks, List<HoldingStock> us_stocks)
        {
            //isUpdated = false;

            stockBuy = 0;
            stockValue = 0;
            stockProfit = 0;

            foreach (HoldingStock stock in kor_stocks)
            {
                stockBuy += stock.buyPrice;
                stockValue += stock.evaluatePrice;
                stockProfit += stock.evaluateProfit;
            }

            stockBuyUs = 0;
            stockValueUs = 0;
            stockProfitUs = 0;

            foreach (HoldingStock stock in us_stocks)
            {
                stockBuyUs += stock.buyPrice;
                stockValueUs += stock.evaluatePrice;
                stockProfitUs += stock.evaluateProfit;
            }

            isUpdated = true;
        }

    }
}
