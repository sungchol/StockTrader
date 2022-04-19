using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeTestCs
{
    public struct STOCKPRICE
    {
        public string date;
        public uint price, price_start, price_high, price_low;  // , foriegn;
        public uint volume;
        public float rate;
        public float ma5, ma20, ma60;
        public float macd12, macd26, macd, macd_signal;
    }

    public class TradeStock
    {
        public string code;
        public string name;
        public string buyDate;
        public string sellDate;
        public uint buyPrice, buyQuantity;
        public uint sellPrice, sellQuantity;
    }

    public enum Method { Ma_Short = 1, Ma_Middle, Ma_Long };

    public class StockData
    {
        public string stockCode;
        public string stockName;
        public STOCKPRICE[] stockPrice = new STOCKPRICE[240];
        public float ma60_FarRate, ma60_MidRate, ma60_CurRate;
        public float ma20_FarRate, ma20_MidRate, ma20_CurRate;
        public float day_FarRate, day_MidRate, day_CurRate;

        public void Ma_day_Price(int day, Method method)
        {
            int i, nMaxcount = 240;
            uint sumPrice = 0;

            for (i = 0; i < day; i++)
            {
                if (this.stockPrice[i].price == 0) return;
                sumPrice += stockPrice[i].price;
            }

            for (i = 0; i < nMaxcount - day; i++)
            {
                if (this.stockPrice[i + day - 1].price == 0) break; //마지막이 0이면 멈춤
                if (i > 0)
                {
                    sumPrice += this.stockPrice[i + day - 1].price - this.stockPrice[i - 1].price;
                }
                switch (method)
                {

                    case Method.Ma_Short:
                        this.stockPrice[i].ma5 = ((float)sumPrice / day);
                        break;
                    case Method.Ma_Middle:
                        this.stockPrice[i].ma20 = ((float)sumPrice / day);
                        break;
                    case Method.Ma_Long:
                        this.stockPrice[i].ma60 = ((float)sumPrice / day);
                        break;
                }
            }
        }

        public void Ma0_Update() //int day, Method method)
        {
            int i;
            uint sumPrice = 0;

            for (i = 0; i < 5; i++)
            {
                if (this.stockPrice[i].price == 0) return;
                sumPrice += stockPrice[i].price;
            }
            this.stockPrice[0].ma5 = ((float)sumPrice / 5.0f);

            for (i = 5; i < 20; i++)
            {
                if (this.stockPrice[i].price == 0) return;
                sumPrice += stockPrice[i].price;
            }
            this.stockPrice[0].ma20 = ((float)sumPrice / 20.0f);

            for (i = 20; i < 60; i++)
            {
                if (this.stockPrice[i].price == 0) return;
                sumPrice += stockPrice[i].price;
            }
            this.stockPrice[0].ma60 = ((float)sumPrice / 60.0f);
        }

        public void Macd_Price(int nMa12, int nMa26, int nSinal9)
        {
            int i, nMaxcount = 240;
            int nRealCount;
            float fMacd_ma12, fMacd_ma26, fMacd;

            for (i = 0; i < nMaxcount; i++)
            {
                if (this.stockPrice[i].price == 0) break;
            }
            nRealCount = i - 1;

            if (nRealCount < 27) return;  //실제 자료개수가 26개 이하면 작업안함

            fMacd_ma12 = (float)this.stockPrice[nRealCount].price;
            fMacd_ma26 = (float)this.stockPrice[nRealCount].price;

            for (i = 1; i < nMa26; i++)
            {
                if (this.stockPrice[nRealCount - i].price == 0) return;
                fMacd_ma12 = this.stockPrice[nRealCount - i].price * (2.0f / (nMa12 + 1))
                    + fMacd_ma12 * (1.0f - 2.0f / (nMa12 + 1));
                fMacd_ma26 = this.stockPrice[nRealCount - i].price * (2.0f / (nMa26 + 1))
                    + fMacd_ma26 * (1.0f - 2.0f / (nMa26 + 1));
                //m_StockData.stockPrice[nRealCount - i].macd = fMacd_ma12 - fMacd_ma26;
            }
            this.stockPrice[nRealCount - i].macd12 = fMacd_ma12;
            this.stockPrice[nRealCount - i].macd26 = fMacd_ma26;
            this.stockPrice[nRealCount - i + 1].macd = fMacd_ma12 - fMacd_ma26;
            this.stockPrice[nRealCount - i + 1].macd_signal = fMacd_ma12 - fMacd_ma26;

            for (i = nMa26; i <= nRealCount; i++)
            {
                if (this.stockPrice[nRealCount - i].price == 0) break;

                fMacd_ma12 = this.stockPrice[nRealCount - i].price * (2.0f / (nMa12 + 1))
                    + fMacd_ma12 * (1.0f - 2.0f / (nMa12 + 1));
                fMacd_ma26 = this.stockPrice[nRealCount - i].price * (2.0f / (nMa26 + 1))
                    + fMacd_ma26 * (1.0f - 2.0f / (nMa26 + 1));

                this.stockPrice[nRealCount - i].macd12 = fMacd_ma12;
                this.stockPrice[nRealCount - i].macd26 = fMacd_ma26;

                fMacd = fMacd_ma12 - fMacd_ma26;
                this.stockPrice[nRealCount - i].macd = fMacd;

                this.stockPrice[nRealCount - i].macd_signal = fMacd * (2.0f / (nSinal9 + 1))
                    + this.stockPrice[nRealCount - i + 1].macd_signal * (1.0f - 2.0f / (nSinal9 + 1));
            }

        }

        public void Macd0_Update(int nMa12, int nMa26, int nSinal9)
        {
            
            float macd_ma12, macd_ma26, macd, signal;

            macd_ma12 = (float)this.stockPrice[1].macd12;
            macd_ma26 = (float)this.stockPrice[1].macd26;
            signal = (float)this.stockPrice[1].macd_signal;

            macd_ma12 = this.stockPrice[0].price * (2.0f / (nMa12 + 1))
                + macd_ma12 * (1.0f - 2.0f / (nMa12 + 1));
            macd_ma26 = this.stockPrice[0].price * (2.0f / (nMa26 + 1))
                + macd_ma26 * (1.0f - 2.0f / (nMa26 + 1));

            this.stockPrice[0].macd12 = macd_ma12;
            this.stockPrice[0].macd26 = macd_ma26;
            this.stockPrice[0].macd = macd_ma12 - macd_ma26;
            this.stockPrice[0].macd_signal = 
                this.stockPrice[0].macd * (2.0f / (nSinal9 + 1))
                + signal * (1.0f - 2.0f / (nSinal9 + 1));
            
        }

        public void StockRiseRate()
        {
            //m_cs.Lock();

            if (this.stockPrice[150].ma60 > 0)
                this.ma60_FarRate = (this.stockPrice[100].ma60 / this.stockPrice[150].ma60 - 1.0f) * 100;
            if (this.stockPrice[100].ma60 > 0)
                this.ma60_MidRate = (this.stockPrice[50].ma60 / this.stockPrice[100].ma60 - 1.0f) * 100;
            if (this.stockPrice[150].ma60 > 0)
                this.ma60_CurRate = (this.stockPrice[0].ma60 / this.stockPrice[50].ma60 - 1.0f) * 100;

            if (this.stockPrice[30].ma20 > 0)
                this.ma20_FarRate = (this.stockPrice[20].ma20 / this.stockPrice[30].ma20 - 1.0f) * 100;
            if (this.stockPrice[20].ma20 > 0)
                this.ma20_MidRate = (this.stockPrice[10].ma20 / this.stockPrice[20].ma20 - 1.0f) * 100;
            if (this.stockPrice[10].ma20 > 0)
                this.ma20_CurRate = (this.stockPrice[0].ma20 / this.stockPrice[10].ma20 - 1.0f) * 100;

            if (this.stockPrice[6].price > 0)
                this.day_FarRate = ((float)this.stockPrice[4].price / (float)this.stockPrice[6].price - 1.0f) * 100;
            if (this.stockPrice[4].price > 0)
                this.day_MidRate = ((float)this.stockPrice[2].price / (float)this.stockPrice[4].price - 1.0f) * 100;
            if (this.stockPrice[2].price > 0)
                this.day_CurRate = ((float)this.stockPrice[0].price / (float)this.stockPrice[2].price - 1.0f) * 100;

            //m_cs.Unlock();
        }


        public bool Ma_GoldenCross(int day)
        {
            
            bool isRight = false; 

            //UpdateData();
            if (day + 10 > 30) day = 20;

            //조건검색자료가 부족하다면 검색 중지
            for (int i = 0; i <= day + 1; i++)
            {
                if (stockPrice[i].ma5 == 0) return false;
                if (stockPrice[i].ma20 == 0) return false;
            }

            //day+1 : 단기5 < 중기20 -> day 부터 계속 단기>중기에 해당하는지  
            if(stockPrice[day+1].ma5 < stockPrice[day+1].ma20 )
            {
                isRight = true;

                for(int i=0; i<=day; i++)
                {
                    if (stockPrice[day - i].ma5 <= stockPrice[day - i].ma20)
                    {
                        isRight = false;
                        break;
                    }
                }
            }

            return isRight;

        }


        public bool Ma_DeadCross(int day)
        {
            //3일이내 골든크로스 확인, 


            bool isRight = false;

            //UpdateData();
            if (day + 10 > 30) day = 20;

            //조건검색자료가 부족하다면 검색 중지
            for (int i = 0; i <= day + 1; i++)
            {
                if (stockPrice[i].ma5 == 0) return false;
                if (stockPrice[i].ma20 == 0) return false;
            }

            //day+1 : 단기5 > 중기20 -> day 부터 계속 단기<중기에 해당하는지  
            if (stockPrice[day + 1].ma5 > stockPrice[day + 1].ma20)
            {
                isRight = true;

                for (int i = 0; i <= day; i++)
                {
                    if (stockPrice[day - i].ma5 >= stockPrice[day - i].ma20)
                    {
                        isRight = false;
                        break;
                    }
                }
            }

            return isRight;

        }

        //day이후 계속 +인경우(즉 이평골드크로스 유지상황)
        public bool Macd_Plus(int day)
        {
            //3일이내 osillator plus, macd plus인 경우
            bool bResult = true;

            if (day > 28) day = 28;

            for (int i = 0; i < day + 1; i++)
            {
                if (stockPrice[i].macd <= 0)
                {
                    bResult = false;
                    break;
                }
            }

            return bResult;
        }

        public bool Macd_Minus(int day)
        {
            //3일이내 osillator plus, macd plus인 경우
            bool bResult = true;

            if (day > 28) day = 28;

            for (int i = 0; i < day + 1; i++)
            {
                if (stockPrice[i].macd >= 0)
                {
                    bResult = false;
                    break;
                }
            }

            return bResult;
        }

        public bool Macd_GoldenCross(int day)
        {
            //3일이내 osillator plus, macd plus인 경우
            bool isRight = false;
            
            if (day > 28) day = 28;

            //day+1 : macd < signal, day 이후 macd > signal
            if (stockPrice[day + 1].macd < stockPrice[day + 1].macd_signal)
            {
                isRight = true;
                for (int i = 0; i <= day; i++)
                {
                    if (stockPrice[day - i].macd <= stockPrice[day - i].macd_signal)
                    {
                        isRight = false;
                        break;
                    }
                }
            }

            return isRight;
        }

        public bool Macd_DeadCross(int day)
        {
            //3일이내 osillator plus, macd plus인 경우
            bool isRight = false;

            if (day > 28) day = 28;

            //day+1 : macd > signal, day 이후 계속 macd < signal
            if (stockPrice[day + 1].macd > stockPrice[day + 1].macd_signal)
            {
                isRight = true;
                for (int i = 0; i <= day; i++)
                {
                    if (stockPrice[day - i].macd >= stockPrice[day - i].macd_signal)
                    {
                        isRight = false;
                        break;
                    }
                }
            }

            return isRight;
        }

        public object[] MakeRow(int index)
        {
            
            object[] rowData = new object[17];
            STOCKPRICE price = stockPrice[index];

            rowData[0] = 0;
            rowData[1] = stockCode;
            rowData[2] = stockName;

            rowData[3] = price.date;
            rowData[4] = Util.Comma(price.price);
            rowData[5] = Util.Comma(price.price_high);
            rowData[6] = Util.Comma(price.price_low);
            rowData[7] = Util.Comma(price.rate);

            rowData[8] = Util.Comma(price.ma5);
            rowData[9] = Util.Comma(price.ma20);
            rowData[10] = Util.Comma(price.macd);
            rowData[11] = Util.Comma(price.macd_signal);

            rowData[12] = Util.Comma(ma60_FarRate);
            rowData[13] = Util.Comma(ma60_MidRate);
            rowData[14] = Util.Comma(ma60_CurRate);
            rowData[15] = Util.Comma(ma20_CurRate);
            rowData[16] = Util.Comma(day_CurRate);
            

            return rowData;
        }
    }

    

    
}
