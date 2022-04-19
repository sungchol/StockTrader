using AxITGExpertCtlLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeTestCs
{
    class Util
    {
        public static string Comma(int num)
        {
            return string.Format("{0:#,##0}", num);
        }

        public static string Comma(uint num)
        {
            return string.Format("{0:#,##0}", num);
        }

        public static string Comma(double num, bool isDecimal = false)
        {
            if(!isDecimal)
            {
                return string.Format("{0:#,##0}", num);
            }
            else
            {
                return string.Format("{0:#,##0.00}", num);
            }
            
        }

        public static string Comma(float num)
        {
            return string.Format("{0:#,##0.00}", num);
        }


        public static void PrintReceiveData(AxITGExpertCtl axITGExpertCtl1)
        {
            int field = axITGExpertCtl1.GetMultiFieldCount(0, 0);
            int record = axITGExpertCtl1.GetMultiRecordCount(0);
            int block = axITGExpertCtl1.GetMultiBlockCount();

            string str = String.Format("BLOCK {0} RECORD {1} Field {2}", block, record, field);
            Console.WriteLine(str);
            for (short k = 0; k < block; k++)
            {
                record = axITGExpertCtl1.GetMultiRecordCount(k);
                for (short j = 0; j < record; j++)
                {
                    field = axITGExpertCtl1.GetMultiFieldCount(k, j);
                    for (short i = 0; i < field; i++)
                    {
                        Console.Write("{0},{1},{2}: {3} \n", k, j, i, axITGExpertCtl1.GetMultiData(k, j, i, 0));
                    }
                    Console.WriteLine();
                }
            }

            field = axITGExpertCtl1.GetSingleFieldCount();
            for (short i = 0; i < field; i++)
            {
                Console.Write("{0}: {1} \n", i, axITGExpertCtl1.GetSingleData(i, 0));
            }

        }

        public static void PrintWatchStocks(List<WatchStock> stocks)
        {
            int i = 0;
            foreach (WatchStock stock in stocks)
            {
                Console.WriteLine("{0} : {1}", i++, stock.code);
            }
        }

        public static void printStockData(StockData stockData)
        {
            Console.WriteLine("{0} {1}", stockData.stockName, 
                stockData.stockCode);
            foreach (STOCKPRICE price in stockData.stockPrice)
            {
                Console.WriteLine("{0} {1:C} {2:N} {3:N} {4:N2}", 
                    price.date, price.price, price.price_start, price.price_high, price.macd);
            }

        }

        public static void PrintTradeStocks(List<TradeStock> stocks)
        {
            int i = 0;
            foreach (TradeStock stock in stocks)
            {
                Console.WriteLine("{0} : {1}, {2} {3}, {4} {5}", i++, stock.name,
                    stock.buyDate, stock.buyPrice, stock.sellDate, stock.sellPrice);
            }
        }

        //바이트단위로 문자열을 자른다
        public static string strmid(string line, int start, int length)
        {
            byte[] str = Encoding.Default.GetBytes(line);
            return Encoding.Default.GetString(str, start, length);
    
        }


    }
}
