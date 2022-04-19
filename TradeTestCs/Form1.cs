using AxITGExpertCtlLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Console;

namespace TradeTestCs
{
    public partial class Form1 : Form
    {
        //Form1 주소정보, 다른클래스에서 Form1 접근하는 통로 Get()
        private static Form1 instance = null;

        //today
        int year, month, day;
        string startDay, endDay;

        //계좌정보 : 개수, 번호, 상품번호
        int accountCount = 0;
        string account;
        string account_sub;
        string password;
        string id, pw;

        MyAsset myAsset = new MyAsset();

        //현재보유종목, 감시종목
        List<HoldingStock> holdingStocks;
        List<HoldingStock> holdingUsStocks;
        List<WatchStock> watchStocks;
        List<WatchStock> watchUsStocks;
        List<StockList> stockLists;
        List<StockUsList> stockUsLists;
        List<StockData> stockDatas;
        List<TradeStock> tradeStocks;
        List<string> findCodes;
        List<string> findNames;
        List<string> buyCodes = new List<string>();
        StockData minuteChartStock;


        //감시종목 Flag 설정
        public bool isWatchUsStockUpdate;
        public bool isWatchStockUpdate;
        public bool isWatchStockPriceChanged;
        public bool isWatchUsStockPriceChanged;
        public bool isStockDataUpdate;
        public Queue<int> updatedStockData = new Queue<int>();
        public Queue<int> updatedWatchStockPrice = new Queue<int>();
        public bool isStartTrade = false;

        //Api Expert Flag설정
        public bool[] isApiBusy = new bool[11];
        public bool[] isApiRealBusy = new bool[11];

        public bool[] isApiUpdate = new bool[11];
        public bool isRealPriceRun = false;

        //현재가 조회중인 종목
        public string checkCode, checkName;
        public int checkIndex;

        //차트설정
        int drawChartStockIndex;
        int viewMode = 20;
        bool isFirst = true;
        int searchTimes = 0;  //분봉 연속 몇번조회인지 기록

        //반복작업을 수행할 쓰레드
        RequestManger requestManager;
        Thread updateThread;
        bool isRunning;
        Thread scpThread = null;
        bool isScpRunning;


        //시간관리
        long time0, time1, dt;

        //DataGridView 설정
        SortOrder sorted = SortOrder.None;
        int sortColumn = -1;

        //자동채우기 Collection
        AutoCompleteStringCollection autoComplete = new AutoCompleteStringCollection();
        AutoCompleteStringCollection autoCompleteUs = new AutoCompleteStringCollection();

        public static Form1 Get()
        {
            return instance;
        }

        public Form1()
        {
            InitializeComponent();
            instance = this;

            //today
            DateTime dateTime = DateTime.Now;
            year = dateTime.Year;
            month = dateTime.Month;
            day = dateTime.Day;
            startDay = String.Format("{0}{1:00}{2:00}", year-1, month, (day-1)>0?day-1:day);
            endDay = String.Format("{0}{1:00}{2:00}", year, month, day);

            Console.WriteLine($"{startDay} {endDay}");

            holdingStocks = new List<HoldingStock>();
            holdingUsStocks = new List<HoldingStock>();
            watchStocks = new List<WatchStock>();
            watchUsStocks = new List<WatchStock>();
            stockLists = new List<StockList>();
            stockUsLists = new List<StockUsList>();
            stockDatas = new List<StockData>();
            tradeStocks = new List<TradeStock>();
            findCodes = new List<string>();
            findNames = new List<string>();

            //Request Manager Thread
            requestManager = RequestManger.Get();
            

        }


        //계좌조회, 업데이트
        private void button1_Click(object sender, EventArgs e)
        {
            
            InitTrader();

        }


        private void Form1_Load(object sender, EventArgs e)
        {
            //계좌조회하여 콤보상자에 추가
            
            Console.WriteLine("Form load [Thread Id : {0}]", Thread.CurrentThread.ManagedThreadId);
            

            InitGrid();
            InitiChart();

            //화면업데이트 쓰레드 : 자료변경시 작업실행 Ui반영
            updateThread = new Thread(UpdateThread);
            updateThread.Start();

        }

        void InitTrader()
        {
            //id pw 설정후 조회
            id = txtId.Text;
            pw = txtPw.Text;

            //계좌조회
            SetAccount();

            //거래체결통보요청 SCN_R (모의 VSCN_R)
            axITGExpertCtl2.RequestRealData("SCN_R", id);

            SearchAccount();  //국내주식
            SearchUsDeposit();   //미국예수금
            SearchUsAccount();  //미국주식

            //RequestManager Thread에 task 등록
            requestManager.RegistTask(new Task(() => SearchAccount()));  //국내주식
            requestManager.RegistTask(new Task(SearchUsDeposit));   //미국예수금
            requestManager.RegistTask(new Task(SearchUsAccount));  //미국주식


            //실시간 시세조회 종목설정 1개 그룹당 Max50개 제한
            //현재보유종목 + 사용자등록 + 조건해당종목..



            //실시간 주식체결정보 SC_R
            //axITGExpertCtl4.RequestRealData("SC_R", "005930   ");

            //requestManager.RegistTask(new Task(() =>
            // {
            //  //axITGExpertCtl2.RequestRealData("OS_STCNT0_R", "DNASTQQQ        DNASAAPL        ");
            //    axITGExpertCtl4.RequestRealData("OS_STCNT0_R", "DNASTQQQ        DNASAAPL        DNASMSFT        ");
            //}));


            //requestManager.RegistTask(new Task(() =>
            //{

            //    //OS_ST01 : 해외주식일자별 주가조회
            //    axITGExpertCtl4.SetSingleData(0, "");
            //    axITGExpertCtl4.SetSingleData(1, "NAS");
            //    axITGExpertCtl4.SetSingleData(2, "TQQQ");
            //    //axITGExpertCtl1.RequestData("SATPS");
            //    axITGExpertCtl4.RequestData("OS_ST03");
            //}));
            MakeStockList();

            //requestManager.RegistTask(new Task(() => 
            //    LookForNowPrice("005930")));

            //requestManager.RegistTask(new Task(() =>
            LookForNowUsPrice("TQQQ");
        }

        //현재가 조회 SCP : 현재가 거래량 52주 고가 저가 per 등 정보제공
        public void LookForNowPrice(string code)
        {
            axITGExpertCtl6.SetSingleData(0, "J");
            axITGExpertCtl6.SetSingleData(1, code);
            axITGExpertCtl6.RequestData("SCP");
            isApiBusy[6] = true;
        }

        //미국주식 현재가 조회 OS_ST01
        public void LookForNowUsPrice(string code)
        {

            
            //while (isRunning)
            {
                //권한조회
                //axITGExpertCtl5.SetSingleData(0, axITGExpertCtl1.GetOverSeasStockSise().ToString());
                axITGExpertCtl5.SetSingleData(0, "DDDDDDDDDD");
                axITGExpertCtl5.SetSingleData(1, "NAS");
                axITGExpertCtl5.SetSingleData(2, code);
                axITGExpertCtl5.RequestData("OS_ST01");
                WriteLine("해외시세조회요청");
                Thread.Sleep(200);
                Application.DoEvents();
            }
            

        }


        //시스템에서 종목리스트 읽어오기 (일정규모이상목록) 
        public void MakeStockList()
        {
            //int i = 0;
            string path = "C:/eFriend Expert/Common/master/kospi_code.mst";
            string[] lines = null;
            string str;

            string code = "", name;
            string stockType, stop;
            UInt32 totalValue; //, nProfit;


            //코스피읽어오기

            try
            {
                lines = File.ReadAllLines(path, Encoding.Default);
            }
            catch (IOException e)
            {
                WriteLine("파일오류[{0}] : {1} ", path, e.Message);
                return;
            }

            foreach (string line in lines) // 한줄씩 읽어오기
            {
                code = Util.strmid(line, 0, 9).Trim();
                name = Util.strmid(line, 21, 40).Trim();
                stockType = Util.strmid(line, 61, 2);
                //strProfit = line.Substring(233,9);
                //nProfit = atoi(strProfit);
                str = Util.strmid(line, 273, 9);

                totalValue = UInt32.Parse(str);
                stop = line.Substring(121, 3);

                //주식인 경우 시가총액 3조원 이상 거래정지아닌 종목, etf인 경우
                if (!stop.Contains("Y") && stockType.CompareTo("ST") == 0 || stockType.Equals("EF")) 
                {
                    //주식목록에 추가
                    autoComplete.Add(name + "\t" + code);
                    
                    //if(stockType.Equals("ST") && nProfit >= 100) || strcmp(stockType, "EF") == 0)
                    //시가총액 3조이상은 관심종목에
                    if(totalValue >= 3000)
                        stockLists.Add(new StockList(name, code, totalValue));
                }
            }
            

            //코스닥 주식명단 읽어오기
            
            try
            {
                path = "C:/eFriend Expert/Common/master/kosdaq_code.mst";
                lines = File.ReadAllLines(path, Encoding.Default);
            }
            catch (IOException e)
            {
                WriteLine("파일오류[{0}] : {1} ", path, e.Message);
                return;
            }

            foreach (string line in lines) // 한줄씩 읽어오기
            {
                code = Util.strmid(line, 0, 9).Trim();
                name = Util.strmid(line, 21, 40).Trim();
                stockType = Util.strmid(line, 61, 2);
                //strProfit = line.Substring(233,9);
                //nProfit = atoi(strProfit);
                str = Util.strmid(line, 267, 9);

                totalValue = uint.Parse(str);
                stop = line.Substring(116, 3);

                //거래정지주식인 아닌경우  시가총액 1조원 이상
                if (!stop.Contains("Y") && stockType.CompareTo("ST") == 0)
                {
                    //목록추가
                    autoComplete.Add(name + "\t" + code);

                    if(totalValue >= 3000)
                        stockLists.Add(new StockList(name, code, totalValue));
                }
            }
            
            stockLists.Sort((x, y) => {
                return (int)(y.Value - x.Value);
            });

            
            //foreach (StockList list in stockLists)
            //{
            //    WriteLine("{0},{1},{2},{3}", i++, list.Code, list.Name,
            //        list.Value);
            //}

            WriteLine("{0} {1}", stockLists.Count, stockLists.Capacity);

            txtWatchCode.AutoCompleteCustomSource = autoComplete;
            txtWatchCode.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            txtWatchCode.AutoCompleteSource = AutoCompleteSource.CustomSource;

            txtStockList.AutoCompleteCustomSource = autoComplete;
            txtStockList.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            txtStockList.AutoCompleteSource = AutoCompleteSource.CustomSource;

            //////////////////////////////////////////////
            ////미국나스닥
            try
            {
                path = "C:/eFriend Expert/Common/master/nasmst.cod";
                lines = File.ReadAllLines(path, Encoding.Default);
            }
            catch (IOException e)
            {
                WriteLine("파일오류[{0}] : {1} ", path, e.Message);
                return;
            }

            foreach (string line in lines) // 한줄씩 읽어오기
            {
                string[] items = line.Split('\t');

                code = items[4].Trim();
                name = items[6].Trim();
                string market = items[2].Trim();
                string kinds = items[8].Trim();

                //if (kinds.Equals("2") || kinds.Equals("3"))
                if (code == "TQQQ" || code == "SQQQ")
                {
                    //목록추가
                    stockUsLists.Add(new StockUsList(name, code, market));
                    //autoCompleteUs.Add(code+ "\t"+ name + "\t" + market);
                }
            }

            ////////////////////////////////////////////
            //미국뉴욕
            try
            {
                path = "C:/eFriend Expert/Common/master/nysmst.cod";
                lines = File.ReadAllLines(path, Encoding.Default);
            }
            catch (IOException e)
            {
                WriteLine("파일오류[{0}] : {1} ", path, e.Message);
                return;
            }

            foreach (string line in lines) // 한줄씩 읽어오기
            {
                string[] items = line.Split('\t');

                code = items[4].Trim();
                name = items[6].Trim();
                string market = items[2].Trim();
                string kinds = items[8].Trim();

                //목록추가
                //if (kinds.Equals("2") || kinds.Equals("3"))
                if(code == "TQQQ" || code == "SQQQ")
                {
                    stockUsLists.Add(new StockUsList(name, code, market));
                    //autoCompleteUs.Add(code + "\t" + name + "\t" + market);
                }

            }

            stockUsLists.Sort((x, y) =>
            {
                return x.Code.CompareTo(y.Code); //오름차순정렬
            });

            foreach (StockUsList list in stockUsLists)
            {
                //WriteLine("{0},{1},{2},{3}", i++, list.Code, list.Name,
                //    list.Market);
                autoCompleteUs.Add(list.Code + "\t" + list.Name + "\t" + list.Market);
            }

            WriteLine("stocklist size {0} capacity {0}", stockUsLists.Count,
                stockUsLists.Capacity);

            txtWatchUsCode.AutoCompleteCustomSource = autoCompleteUs;
            txtWatchUsCode.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            txtWatchUsCode.AutoCompleteSource = AutoCompleteSource.CustomSource;
        }


        //반복작업 UI업데이트, 기타반복필요한 것
        public void UpdateThread()
        {
            isRunning = true;
            long tradeTimer = 0, tradeTime = 30000;

            time0 = time1 = DateTime.Now.Ticks;
            dt = (time1 - time0) / 10000;

            while (isRunning)
            {
                try
                {
                    tradeTimer += dt;

                    //국내주식 그리드뷰 다시표시하기
                    if (isApiUpdate[1])
                    {
                        DisplayHoldStock(dataGridView1, holdingStocks);
                        isApiUpdate[1] = false;
                        myAsset.CalulateAsset(holdingStocks, holdingUsStocks);
                        Console.WriteLine("Api1 update Thread Id :" + Thread.CurrentThread.ManagedThreadId);
                        
                        MakeListWatch();

                    }

                    //해외주식 그리드뷰 다시표시하기
                    if (isApiUpdate[3])
                    {
                        DisplayHoldStock(dataGridView2, holdingUsStocks, true);
                        isApiUpdate[3] = false;
                        myAsset.CalulateAsset(holdingStocks, holdingUsStocks);
                        Console.WriteLine("Api3 update Thread Id :" + Thread.CurrentThread.ManagedThreadId);

                        MakeListUsWatch();
                    }

                    //myAsset 잔액업데이트 : 현재가변경
                    if (myAsset.isUpdated)
                    {
                        DisplayAsset();
                        myAsset.isUpdated = false;
                        Console.WriteLine("Asset Ui update Thread Id :" + Thread.CurrentThread.ManagedThreadId);
                    }

                    //감시목록 업데이트
                    if (isWatchUsStockUpdate)
                    {
                        DisplayWatchStock(dataGridView4, watchUsStocks);
                        isWatchUsStockUpdate = false;
                        //StartWatchUsStock();
                        Console.WriteLine("미국감시목록 Ui update Thread Id :" + Thread.CurrentThread.ManagedThreadId);
                    }

                    //국내감시목록 업데이트
                    if (isWatchStockUpdate)
                    {
                        DisplayWatchStock(dataGridView3, watchStocks);
                        DisplayWatchStock(dataGridView6, watchStocks);
                        isWatchStockUpdate = false;
                        //StartWatchStock();
                        Console.WriteLine("국내감시목록 Ui update Thread Id :" + Thread.CurrentThread.ManagedThreadId);

                        //국내감시종목 240일 상세내역 구축하기
                        UpdateStockData();
                    }

                    //데이타그리드뷰 및 holdingStock 업그레이드
                    //현재가 조회한 후 stockdatas 및 그리드 업데이트
                    //if(isWatchStockPriceChanged)
                    if(updatedWatchStockPrice.Count > 0)
                    {
                        int gridRow = dataGridView3.RowCount - 1;
                        int index = updatedWatchStockPrice.Dequeue();

                        WatchStock stock = watchStocks[index];
                        //foreach(WatchStock stock in watchStocks)
                        
                        //그리드3, 6업데이트 현재가 상승율 리스트
                        //if(stock.isChanged)
                        {
                            string code = stock.code;
                            for (int i = 0; i < gridRow; i++)
                            {
                                if (dataGridView3[1, i].Value.ToString() == code)
                                {
                                    dataGridView3[2, i].Value = stock.price0;
                                    dataGridView3[3, i].Value = stock.price1;
                                    break;
                                }
                            }

                            for (int i = 0; i < gridRow; i++)
                            {
                                if (dataGridView6[1, i].Value.ToString() == code)
                                {
                                    dataGridView6[3, i].Value = stock.price1;
                                    dataGridView6[2, i].Value = float.Parse(Util.Comma(stock.rate));
                                    break;
                                }
                            }

                            stock.isChanged = false;
                        }

                        //stockdata grid view 업데이트
                        StockData stockData = stockDatas[index];
                        stockData.Macd0_Update(12, 26, 9);
                        stockData.Ma0_Update();

                        gridRow = dataGridView7.RowCount - 1;
                        if (gridRow > 2)
                        {
                            if (dataGridView7[1, 0].Value.ToString() == stockData.stockCode)
                            {
                                dataGridView7[4, 0].Value = Util.Comma(stockData.stockPrice[0].price); 
                                dataGridView7[5, 0].Value = Util.Comma(stockData.stockPrice[0].price_high);  //고가
                                dataGridView7[6, 0].Value = Util.Comma(stockData.stockPrice[0].price_low);
                                dataGridView7[7, 0].Value = Util.Comma(stockData.stockPrice[0].rate);
                                dataGridView7[8, 0].Value = Util.Comma(stockData.stockPrice[0].ma5);
                                dataGridView7[9, 0].Value = Util.Comma(stockData.stockPrice[0].ma20);
                                dataGridView7[10, 0].Value = Util.Comma(stockData.stockPrice[0].macd);
                                dataGridView7[11, 0].Value = Util.Comma(stockData.stockPrice[0].macd_signal);

                                //Console.WriteLine("{0} StockData값을 그리드7에 업데이트합니다 ID : {1}", stockData.stockName,  Thread.CurrentThread.ManagedThreadId);
                                

                            }

                        }

                        gridRow = dataGridView5.RowCount - 1;
                        if (gridRow > 2)
                        {
                            if (dataGridView5[1, 0].Value.ToString() == stockData.stockCode)
                            {
                                dataGridView5[4, 0].Value = Util.Comma(stockData.stockPrice[0].price);
                                dataGridView5[5, 0].Value = Util.Comma(stockData.stockPrice[0].price_high);  //고가
                                dataGridView5[6, 0].Value = Util.Comma(stockData.stockPrice[0].price_low);
                                dataGridView5[7, 0].Value = Util.Comma(stockData.stockPrice[0].rate);
                                dataGridView5[8, 0].Value = Util.Comma(stockData.stockPrice[0].ma5);
                                dataGridView5[9, 0].Value = Util.Comma(stockData.stockPrice[0].ma20);
                                dataGridView5[10, 0].Value = Util.Comma(stockData.stockPrice[0].macd);
                                dataGridView5[11, 0].Value = Util.Comma(stockData.stockPrice[0].macd_signal);
                                //Console.WriteLine("{0} StockData값을 그리드5에 업데이트합니다 ID : {1}", stockData.stockName, Thread.CurrentThread.ManagedThreadId);

                            }

                        }
                        isWatchStockPriceChanged = false;
                    }

                    //stockData [240]조회가 완료된 경우, ma, macd 자료구축
                    if (updatedStockData.Count > 0)
                    {
                        
                        int index = updatedStockData.Dequeue();
                        StockData stockData = stockDatas[index];
                        stockData.Ma_day_Price(5, Method.Ma_Short);
                        stockData.Ma_day_Price(20, Method.Ma_Middle);
                        stockData.Ma_day_Price(60, Method.Ma_Long);
                        stockData.Macd_Price(12, 26, 9);
                        stockData.StockRiseRate();

                        if (isFirst)
                        {
                            
                            drawChartStockIndex = index;

                            if (checkChart.Checked)
                            {
                                DrawChart(stockData, viewMode);
                            }
                            isFirst = false;
                        }

                        watchStocks[index].price0 = (int) stockData.stockPrice[0].price;
                        watchStocks[index].rate = stockData.stockPrice[0].rate;
                        watchStocks[index].isChanged = true;
                        //isWatchStockPriceChanged = true;
                        //updatedWatchStockPrice.Enqueue(index);
                        //Console.WriteLine($"주식자료업데이트({stockDatas[index].stockName})(macd) Thread Id : " + Thread.CurrentThread.ManagedThreadId);
                        
                        //그리드뷰업데이트
                        WatchStock stock = watchStocks[index];
                        int gridRow = dataGridView3.RowCount - 1;
                        string code = stock.code;
                        for (int i = 0; i < gridRow; i++)
                        {
                            if (dataGridView3[1, i].Value.ToString() == code)
                            {
                                dataGridView3[2, i].Value = stock.price0;
                                dataGridView3[3, i].Value = stock.price1;
                                break;
                            }
                        }

                        for (int i = 0; i < gridRow; i++)
                        {
                            if (dataGridView6[1, i].Value.ToString() == code)
                            {
                                dataGridView6[3, i].Value = stock.price1;
                                dataGridView6[2, i].Value = Util.Comma(stock.rate);
                                break;
                            }
                        }
                        stock.isChanged = false;
                    }

                    //stockDatas 업데이트가 되면 현재가 조회시작
                    if(isStockDataUpdate)
                    {
                        if (scpThread != null)
                            scpThread.Interrupt();

                        Console.WriteLine("SCP 조회를 시작합니다 쓰레드: {0}",
                            Thread.CurrentThread.ManagedThreadId);
                        UpdateCurrentPriceThread();
                        isStockDataUpdate = false;
                    }


                    if (isStartTrade && tradeTimer > tradeTime)
                    {
                        tradeTimer = 0;
                        StartTrade();
                        Console.WriteLine("30초 경과" + dt);
                    }

                    //watchStock list 가격변동액 UI 및 holdingStocks 반영


                    Thread.Sleep(20);

                    time1 = DateTime.Now.Ticks;
                    dt = (time1 - time0) / 10000;
                    time0 = time1;
                }
                catch (ThreadInterruptedException e)
                {
                    isRunning = false;
                    Console.WriteLine("Update Stop : " + e.Message);
                    break;
                }

            }
        }

        private void InitGrid()
        {
            //국내주식 잔고
            dataGridView1.ColumnCount = 12;
            dataGridView1.Columns[0].Name = "번호";
            dataGridView1.Columns[1].Name = "코드번호";
            dataGridView1.Columns[2].Name = "주식명";
            dataGridView1.Columns[3].Name = "보유수량";
            dataGridView1.Columns[4].Name = "매입금액";
            dataGridView1.Columns[5].Name = "매입단가";
            dataGridView1.Columns[6].Name = "현재가액";
            dataGridView1.Columns[7].Name = "평가금액";
            dataGridView1.Columns[8].Name = "평가손익";
            dataGridView1.Columns[9].Name = "손익율";
            dataGridView1.Columns[10].Name = "실현손익";
            dataGridView1.Columns[11].Name = "정산손익";

            dataGridView1.RowHeadersVisible = false;
            dataGridView1.ColumnHeadersDefaultCellStyle.Alignment = 
                DataGridViewContentAlignment.MiddleCenter;

            foreach(DataGridViewColumn column in dataGridView1.Columns)
            {
                //사용자가 정의한 sort를 실행할 경우 sortGraph표시는 코딩으로 정해주어야 함
                column.SortMode = DataGridViewColumnSortMode.Programmatic;
            }
            
            for (int i = 3; i < 12; i++)
            {
                dataGridView1.Columns[i].DefaultCellStyle.Alignment = 
                    DataGridViewContentAlignment.MiddleRight;
            }

            //해외주식잔고
            dataGridView2.ColumnCount = 12;
            dataGridView2.Columns[0].Name = "번호";
            dataGridView2.Columns[1].Name = "코드번호";
            dataGridView2.Columns[2].Name = "주식명";
            dataGridView2.Columns[3].Name = "보유수량";
            dataGridView2.Columns[4].Name = "매입금액";
            dataGridView2.Columns[5].Name = "매입단가";
            dataGridView2.Columns[6].Name = "현재가액";
            dataGridView2.Columns[7].Name = "평가금액";
            dataGridView2.Columns[8].Name = "평가손익";
            dataGridView2.Columns[9].Name = "손익율";
            dataGridView2.Columns[10].Name = "실현손익";
            dataGridView2.Columns[11].Name = "정산손익";
            dataGridView2.RowHeadersVisible = false;

            foreach (DataGridViewColumn column in dataGridView2.Columns)
            {
                //사용자가 정의한 sort를 실행할 경우 sortGraph표시는 코딩으로 정해주어야 함
                column.SortMode = DataGridViewColumnSortMode.Programmatic;
            }

            for (int i = 3; i < 12; i++)
            {
                dataGridView2.Columns[i].DefaultCellStyle.Alignment =
                    DataGridViewContentAlignment.MiddleRight;
            }
            dataGridView2.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView1_ColumnHeaderMouseClick);

            //국내감시종목 현황판
            dataGridView3.ColumnCount = 5;
            dataGridView3.Columns[0].Name = "주식명";
            dataGridView3.Columns[1].Name = "코드번호";
            dataGridView3.Columns[2].Name = "직전가";
            dataGridView3.Columns[3].Name = "현재가";
            dataGridView3.Columns[4].Name = "Changed";
            dataGridView3.RowHeadersVisible = false;

            //미국주식 감시종목
            dataGridView4.ColumnCount = 5;
            dataGridView4.Columns[0].Name = "시장명";
            dataGridView4.Columns[1].Name = "코드번호";
            dataGridView4.Columns[2].Name = "직전가";
            dataGridView4.Columns[3].Name = "현재가";
            dataGridView4.Columns[4].Name = "Changed";
            dataGridView4.RowHeadersVisible = false;

            //국내주식 상세내역
            dataGridView5.ColumnCount = 17;
            dataGridView5.Columns[0].Name = "번호";
            dataGridView5.Columns[1].Name = "코드번호";
            dataGridView5.Columns[2].Name = "종목명";
            dataGridView5.Columns[3].Name = "거래일자";
            dataGridView5.Columns[4].Name = "현재가";
            dataGridView5.Columns[5].Name = "고가";
            dataGridView5.Columns[6].Name = "저가";
            dataGridView5.Columns[7].Name = "상승율";
            dataGridView5.Columns[8].Name = "MA5";
            dataGridView5.Columns[9].Name = "MA20";
            dataGridView5.Columns[10].Name = "MACD";
            dataGridView5.Columns[11].Name = "SIGNAL";
            dataGridView5.Columns[12].Name = "8~5월";
            dataGridView5.Columns[13].Name = "5~2월";
            dataGridView5.Columns[14].Name = "최근2월";
            dataGridView5.Columns[15].Name = "최근2주";
            dataGridView5.Columns[16].Name = "최근2일";

            dataGridView5.RowHeadersVisible = false;

            //감시종목 list
            dataGridView6.ColumnCount = 5;
            dataGridView6.Columns[0].Name = "종목명";
            dataGridView6.Columns[1].Name = "코드번호";
            dataGridView6.Columns[2].Name = "상승율";
            dataGridView6.Columns[3].Name = "현재가";
            dataGridView6.Columns[4].Name = "Changed";
            dataGridView6.RowHeadersVisible = false;
            dataGridView6.Columns[2].DefaultCellStyle.Alignment =
                    DataGridViewContentAlignment.MiddleRight;
            dataGridView6.Columns[3].DefaultCellStyle.Alignment =
                    DataGridViewContentAlignment.MiddleRight;


            //일자별 상세내역
            dataGridView7.ColumnCount = 17;
            dataGridView7.Columns[0].Name = "번호";
            dataGridView7.Columns[1].Name = "코드번호";
            dataGridView7.Columns[2].Name = "종목명";
            dataGridView7.Columns[3].Name = "거래일자";
            dataGridView7.Columns[4].Name = "현재가";
            dataGridView7.Columns[5].Name = "고가";
            dataGridView7.Columns[6].Name = "저가";
            dataGridView7.Columns[7].Name = "상승율";
            dataGridView7.Columns[8].Name = "MA5";
            dataGridView7.Columns[9].Name = "MA20";
            dataGridView7.Columns[10].Name = "MACD";
            dataGridView7.Columns[11].Name = "SIGNAL";
            dataGridView7.Columns[12].Name = "8~5월";
            dataGridView7.Columns[13].Name = "5~2월";
            dataGridView7.Columns[14].Name = "최근2월";
            dataGridView7.Columns[15].Name = "최근2주";
            dataGridView7.Columns[16].Name = "최근2일";
            dataGridView7.RowHeadersVisible = false;

            //검색결과
            dataGridView8.ColumnCount = 5;
            dataGridView8.Columns[0].Name = "주식명";
            dataGridView8.Columns[1].Name = "코드번호";
            dataGridView8.Columns[2].Name = "해당조건1";
            dataGridView8.Columns[3].Name = "해당조건2";
            dataGridView8.Columns[4].Name = "해당조건3";
            dataGridView8.RowHeadersVisible = false;

        }

        //보유주식 명세, 평가손익 조회 + 원화예수금
        public void SearchAccount()
        {
            //TC8701R : 계좌예수금, 현재보유주식 평가내역조회
            axITGExpertCtl1.SetSingleData(0, account);
            axITGExpertCtl1.SetSingleData(1, account_sub);
            axITGExpertCtl1.SetSingleData(2, password);
            //axITGExpertCtl1.RequestData("SATPS");
            axITGExpertCtl1.RequestData("TC8701R");
        }

        //미국주식 보유명세, 평가손익조회
        public void SearchUsAccount() 
        {
            axITGExpertCtl3.SetSingleData(0, account);
            axITGExpertCtl3.SetSingleData(1, account_sub);
            axITGExpertCtl3.SetSingleData(2, password);
            axITGExpertCtl3.RequestData("OS_US_CBLC");
            //axITGExpertCtl3.RequestData("OS_RP6504R");

        }

        //US외화외수금조회
        public void SearchUsDeposit()
        {
            axITGExpertCtl2.SetSingleData(0, account);
            axITGExpertCtl2.SetSingleData(1, account_sub);
            axITGExpertCtl2.SetSingleData(2, password);
            axITGExpertCtl2.RequestData("OS_CH_DNCL");
        }

        private void DataTableInit()
        {
            //table1 = new DataTable("Table1");
            //table1.Columns.Add(new DataColumn("Name", typeof(string)));
            //table1.Columns.Add(new DataColumn("Age", typeof(int)));

            //DataRow row = table1.NewRow();      
            //row[0] = "sungchol";
            //row[1] = 20;
            //table1.Rows.Add(row);

            //row = table1.NewRow();
            //row[0] = "dongha";
            //row[1] = 19;
            //table1.Rows.Add(row);
            ////table1.AcceptChanges();

            //dataGridView1.DataSource = table1;
            //dataGridView1.RowHeadersVisible = false;
            //dataGridView1.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            //dataGridView1.MultiSelect = false;
            //dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            ////dataGridView1.Dock = DockStyle.Fill;

        }

        private void SetAccount()
        {
            accountCount = axITGExpertCtl1.GetAccountCount();
            
            for (int i = 0; i < accountCount; i++)
            {
                account = (string)axITGExpertCtl1.GetAccount(0);
                account_sub = account.Substring(8, 2);
                account = account.Substring(0, 8);

                accountNum.Items.Add(account);
                accountSub_num.Items.Add(account_sub);
            }

            //계좌수신못한 (accountCount : 0인) 경우에러처리할 것
            WriteLine($"계좌수 {accountCount}");
            if(accountCount == 0)
            {
                MessageBox.Show("계좌를 수신받지 못함; 종료");
                //Dispose(true);
                return;
            }

            //주식_0번째 계좌선택
            accountNum.SelectedIndex = 0;
            accountSub_num.SelectedIndex = 0;
            password = (string)axITGExpertCtl1.GetEncryptPassword(pw);
        }

        //국내보유주식정보 수신받아 저장
        private void axITGExpertCtl1_ReceiveData(object sender, EventArgs e)
        {
            isApiBusy[1] = true;

            Console.WriteLine("axITGExpertCtl1 Received {0} Thread ID{1}", sender.ToString(), Thread.CurrentThread.ManagedThreadId);
            //Util.PrintReceiveData(axITGExpertCtl1);

            holdingStocks.Clear();

            short record = axITGExpertCtl1.GetMultiRecordCount(0);
            for(short i=0; i<record; i++)
            {
                HoldingStock stock = new HoldingStock();
                stock.code = (string)axITGExpertCtl1.GetMultiData(0, i, 0, 0);
                stock.name = (string)axITGExpertCtl1.GetMultiData(0, i, 1, 0);
                stock.amount = int.Parse((string)axITGExpertCtl1.GetMultiData(0, i, 12, 0));
                stock.unitPrice = double.Parse((string)axITGExpertCtl1.GetMultiData(0, i, 13, 0));
                stock.buyPrice = stock.amount * stock.unitPrice;
                stock.nowPrice = double.Parse((string)axITGExpertCtl1.GetMultiData(0, i, 14, 0));
                stock.evaluateProfit = double.Parse((string)axITGExpertCtl1.GetMultiData(0, i, 15, 0));
                stock.evaluatePrice = stock.amount * stock.nowPrice;
                stock.profitRate = double.Parse((string)axITGExpertCtl1.GetMultiData(0, i, 19, 0));
                stock.realizeProfit = double.Parse((string)axITGExpertCtl1.GetMultiData(0, i, 10, 0));
                stock.netRealProfit = double.Parse((string)axITGExpertCtl1.GetMultiData(0, i, 17, 0));

                holdingStocks.Add(stock);

            }

            //정렬하기 평가금액 내림차순
            holdingStocks.Sort((x, y) => {
                return (int)(y.evaluatePrice - x.evaluatePrice);
            });

            //원화예수금(D+2)
            myAsset.depositKor = long.Parse((string)axITGExpertCtl1.GetMultiData(1, 0, 2, 0));

            isApiBusy[1] = false;
            isApiUpdate[1] = true;
            //정보수신을 완료하였으면 화면에 표시해 준다
            //txtDepositKor.Text = Util.Comma(depositKor);
            Console.WriteLine("axITGExpertCtl1 완료 {0} Thread ID{1}", sender.ToString(), Thread.CurrentThread.ManagedThreadId);
        }

        //해외주식 실시간 시세처리
        private void axITGExpertCtl2_ReceiveRealData(object sender, EventArgs e)
        {
            Console.WriteLine("axITGExpertCtl2 Received, Thread ID : {0}",
                Thread.CurrentThread.ManagedThreadId);

            //Util.PrintReceiveData(axITGExpertCtl2);

            Console.WriteLine("{0} {1} {2} {3} {4}\n", axITGExpertCtl2.GetSingleData(0, 0),
                axITGExpertCtl2.GetSingleData(1, 0), axITGExpertCtl2.GetSingleData(7, 0),
                axITGExpertCtl2.GetSingleData(11, 0), axITGExpertCtl2.GetSingleData(14, 0));


        }

        private void accountNum_SelectedIndexChanged(object sender, EventArgs e)
        {
            accountSub_num.SelectedIndex = ((ComboBox)sender).SelectedIndex;
        }

        public void DisplayHoldStock(DataGridView dataGridView, object stocks,
            bool isUsStock = false)
        {
            int row = 0;

            dataGridView.Rows.Clear();

            foreach (HoldingStock stock in (List<HoldingStock>)stocks)
            {
                row++;
                stock.orderNum = row;
                object[] rowArray = (isUsStock) ? stock.MakeRowUsStock() : stock.MakeRow();
                dataGridView.Rows.Add(rowArray);
            }

        }

        private void dataGridView1_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            
            DataGridView dataGridView1= (DataGridView)sender;
            DataGridViewColumn currentColumn = dataGridView1.Columns[e.ColumnIndex];

            //새로운 칼럼이 선택됨 순서 - 내림차순
            if (sortColumn != e.ColumnIndex)
            {
                //if (sorted == SortOrder.None)
                
                sorted = SortOrder.Descending;
                currentColumn.HeaderCell.SortGlyphDirection = SortOrder.Descending;
                if (sortColumn != -1)
                    dataGridView1.Columns[sortColumn].HeaderCell.SortGlyphDirection = SortOrder.None;
                
            }
            else
            {
                if (sorted == SortOrder.Descending)
                {
                    sorted = SortOrder.Ascending;
                    currentColumn.HeaderCell.SortGlyphDirection = SortOrder.Ascending;
                }
                else
                {
                    sorted = SortOrder.Descending;
                    currentColumn.HeaderCell.SortGlyphDirection = SortOrder.Descending;
                }
            }

            sortColumn = e.ColumnIndex;
            dataGridView1.Sort(new RowComparer(sorted, sortColumn));

        }

        private class RowComparer : System.Collections.IComparer
        {
            private static int sortOrderModifier = 1;
            int sortColumn;

            public RowComparer(SortOrder sortOrder, int col)
            {
                this.sortColumn = col;

                if (sortOrder == SortOrder.Descending)
                {
                    sortOrderModifier = -1;
                }
                else if (sortOrder == SortOrder.Ascending)
                {
                    sortOrderModifier = 1;
                }
            }

            public int Compare(object x, object y)
            {
                DataGridViewRow dataGridViewRow1 = (DataGridViewRow)x;
                DataGridViewRow dataGridViewRow2 = (DataGridViewRow)y;

                object dataX = dataGridViewRow1.Cells[sortColumn].Value;
                object dataY = dataGridViewRow2.Cells[sortColumn].Value;

                int CompareResult = 0;

                if (sortColumn >= 3) // && sortColumn <= 9)
                {
                    double com1 = double.Parse(dataX.ToString().Replace(",", ""));
                    double com2 = double.Parse(dataY.ToString().Replace(",", ""));
                    CompareResult = com1 > com2 ? 1 : (com1 == com2 ? 0 : -1);
                    
                }
                else if(sortColumn > 0 && sortColumn <=2)
                {
                    CompareResult = String.Compare(dataX.ToString(), dataY.ToString());
                }
                else
                {
                    //CompareResult = (int)dataX > (int)dataY ? 1 : ((int)dataX == (int)dataY ? 0 : -1);
                    CompareResult = (int)dataX - (int)dataY;
                }
                
                return CompareResult * sortOrderModifier;
            }
        }

        //해외주식 보유내역 조회
        private void axITGExpertCtl3_ReceiveData(object sender, EventArgs e)
        {
            Console.WriteLine("axITGExpertCtl3 recieve {0} Thread ID{1}", sender.ToString(), Thread.CurrentThread.ManagedThreadId);
            isApiBusy[3] = true;

            AxITGExpertCtl expertCtl = (AxITGExpertCtl)sender;
            //Util.PrintReceiveData(expertCtl);

            holdingUsStocks.Clear();

            short record = axITGExpertCtl3.GetMultiRecordCount(0);
            for (short i = 0; i < record; i++)
            {
                string tmp = (string)axITGExpertCtl3.GetMultiData(0, i, 3, 0);
                if (tmp.Length == 0) break;

                HoldingStock stock = new HoldingStock();
                stock.code = (string)axITGExpertCtl3.GetMultiData(0, i, 3, 0);
                stock.name = (string)axITGExpertCtl3.GetMultiData(0, i, 4, 0);
                stock.amount = int.Parse((string)axITGExpertCtl3.GetMultiData(0, i, 8, 0));
                stock.unitPrice = double.Parse((string)axITGExpertCtl3.GetMultiData(0, i, 7, 0));
                stock.buyPrice = double.Parse((string)axITGExpertCtl3.GetMultiData(0, i, 10, 0));
                stock.nowPrice = double.Parse((string)axITGExpertCtl3.GetMultiData(0, i, 12, 0));
                stock.evaluateProfit = double.Parse((string)axITGExpertCtl3.GetMultiData(0, i, 5, 0));
                stock.evaluatePrice = stock.amount * stock.nowPrice;
                stock.profitRate = double.Parse((string)axITGExpertCtl3.GetMultiData(0, i, 6, 0));
                stock.market = (string)axITGExpertCtl3.GetMultiData(0, i, 14, 0);    //마켓정보

                holdingUsStocks.Add(stock);

            }

            //정렬하기 평가금액 내림차순
            holdingUsStocks.Sort((x, y) => {
                return (int)(y.evaluatePrice - x.evaluatePrice);
            });

            isApiBusy[3] = false;
            isApiUpdate[3] = true;

            Console.WriteLine("axITGExpertCtl3 완료 {0} Thread ID{1}", sender.ToString(), Thread.CurrentThread.ManagedThreadId);
        }

        //외화예수금 정보를 받는다
        private void axITGExpertCtl2_ReceiveData(object sender, EventArgs e)
        {
            Console.WriteLine("axITGExpertCtl2 recieve {0} Thread ID{1}", sender.ToString(), Thread.CurrentThread.ManagedThreadId);
            //Console.WriteLine("외화예수금 OS_CH_DNCL");
            //Util.PrintReceiveData(axITGExpertCtl2);
            myAsset.depositUsd = double.Parse((string)axITGExpertCtl2.GetMultiData(0, 1, 6, 0));
            
            myAsset.isUpdated = true;
            Console.WriteLine("axITGExpertCtl2 완료 {0} Thread ID{1}", sender.ToString(), Thread.CurrentThread.ManagedThreadId);
        }

        public void DisplayAsset()
        {
            txtDepositKor.Text = Util.Comma(myAsset.depositKor);
            txtBuy.Text = Util.Comma(myAsset.stockBuy);
            txtValue.Text = Util.Comma(myAsset.stockValue);
            txtProfit.Text = Util.Comma(myAsset.stockProfit);

            txtDepositUsd.Text = Util.Comma(Math.Round(myAsset.depositUsd, 2), true);
            txtBuyUs.Text = Util.Comma(Math.Round(myAsset.stockBuyUs, 2), true);
            txtValueUs.Text = Util.Comma(Math.Round(myAsset.stockValueUs, 2), true);
            txtProfitUs.Text = Util.Comma(Math.Round(myAsset.stockProfitUs, 2), true);
        }

        //국내주식 감시대상목록 추가
        private void button2_Click(object sender, EventArgs e)
        {
            bool isExist = false;

            string[] items = txtWatchCode.Text.Split('\t');

            //dataGridView에 있는 종목이 있는 경우면
            if(items.Length < 2)
            {
                WriteLine("올바른 항목을 선택하세요");
                txtWatchCode.Text = "";
                return;
            }

                
            //그리드상 중복여부  체크
            int nCount = dataGridView3.RowCount - 1;
            for (int i = 0; i < nCount; i++)
            {
                if (items[1].Equals(dataGridView3[1, i].Value.ToString()))
                {
                    isExist = true;
                    WriteLine("목록에 이미 있습니다");
                    break;
                }
            }

            if (items[1].Length > 0 && !isExist)
            {
                WatchStock stock = new WatchStock(items[0].Trim(), items[1].Trim());

                dataGridView3.Rows.Add(stock.MakeRowStock());
                watchStocks.Add(stock);
                WriteLine("목록에 추가하였습니다");
            }
            txtWatchCode.Text = "";

        }

        //국내주식 감시시작
        private void button3_Click(object sender, EventArgs e)
        {
            StartWatchStock();
        }

        //국내 감시종목 리스트작성
        public bool MakeListWatch()
        {
            bool isExist = false;
            bool isUpdate = false;

            //현재보유중인 종목
            if (holdingStocks.Count > 0)
            {
                foreach (HoldingStock stock in holdingStocks)
                {
                    
                    isExist = false;
                    //이미 등록된 종목인지 확인하고 없을 때 입력
                    foreach (WatchStock st in watchStocks)
                    {
                        if (st.Equals(stock.code))
                        {
                            isExist = true;
                            break;
                        }
                    }

                    if (!isExist)
                    {
                        watchStocks.Add(new WatchStock(stock.name, stock.code));
                        isUpdate = true;
                    }
                }

            }

            if (stockLists.Count > 0)
            {
                foreach (StockList stock in stockLists)
                {

                    isExist = false;
                    //이미 등록된 종목인지 확인하고 없을 때 입력
                    foreach (WatchStock st in watchStocks)
                    {
                        if (st.Equals(stock.Code))
                        {
                            isExist = true;
                            break;
                        }
                    }

                    if (!isExist)
                    {
                        watchStocks.Add(new WatchStock(stock.Name, stock.Code));
                        isUpdate = true;
                    }
                }

            }

            isWatchStockUpdate = isUpdate;
            return isUpdate;
        }

        public void DisplayWatchStock(DataGridView dataGridView, object stocks)
        {
            List<WatchStock> watchStocks = (List<WatchStock>) stocks;

            int rows = dataGridView.RowCount;
            Console.WriteLine("관심종목 디스플레이 시작 {0}개", watchStocks.Count);
            foreach (WatchStock list in watchStocks)
            {
                //이미 있는 경우면 추가하지 않는다
                for (int i = 0; i < rows - 1; i++)
                {
                    if (list.code == dataGridView[1, i].Value.ToString())
                    {
                        break;
                    }
                }
                
                dataGridView.Rows.Add(list.MakeRowStock());
            }
        }


        //미국주식 감시목록 추가
        private void button5_Click(object sender, EventArgs e)
        {
            bool isExist = false;

            string[] items = txtWatchUsCode.Text.Split('\t');

            //dataGridView에 있는 종목이 있는 경우면
            if (items.Length < 3)
            {
                WriteLine("올바른 항목을 선택하세요");
                txtWatchUsCode.Text = "";
                return;
            }

            string code = items[0];
            string name = items[2];

            //그리드상 중복여부  체크
            int nCount = dataGridView4.RowCount - 1;
            for (int i = 0; i < nCount; i++)
            {
                if (code.Equals(dataGridView4[1, i].Value.ToString()))
                {
                    isExist = true;
                    WriteLine("목록에 이미 있습니다");
                    txtWatchUsCode.Text = "";
                    break;
                }
               
            }

            if (code.Length > 0 && !isExist)
            {
                WatchStock stock = new WatchStock(name, code);
                dataGridView4.Rows.Add(stock.MakeRowStock());
                WriteLine("목록에 추가하였습니다 {0}", code);
                watchUsStocks.Add(stock);
                txtWatchUsCode.Text = "";

            }

            //현재보유중인 종목
            //MakeListUsWatch();
        }

        public bool MakeListUsWatch()
        {
            bool isExist = false;
            bool isUpdate = false;

            //현재보유중인 종목
            if(holdingUsStocks.Count > 0)
            {
                foreach (HoldingStock stock in holdingUsStocks)
                {
                    WatchStock myStock = new WatchStock();
                    isExist = false;
                    //이미 등록된 종목인지 확인하고 없을 때 입력
                    foreach (WatchStock st in watchUsStocks)
                    {
                        if(st.Equals(stock.code))
                        {
                            isExist = true;
                            break;
                        }
                    }

                    if(!isExist)
                    {
                        myStock.code = stock.code;
                        myStock.name = stock.market.Substring(0, 3);
                        watchUsStocks.Add(myStock);

                        isUpdate = true;
                    }
                }
            
            }

            //Console.WriteLine("관심종목이 등록되었다 {0}개", watchUsStocks.Count);
            isWatchUsStockUpdate = isUpdate;
            return isUpdate;
        }

        //public void DisplayWatchUsStock()
        //{
        //    int rows = dataGridView4.RowCount;

        //    foreach (WatchStock list in watchUsStocks)
        //    {
        //        //이미 있는 경우면 추가하지 않는다
        //        for(int i = 0; i < rows-1 ; i++)
        //        {
        //            if(list.code == dataGridView4[1, i].Value.ToString())
        //            {
        //                break;
        //            }
        //        }
                
        //        dataGridView4.Rows.Add(list.MakeRowStock());
        //    }
        //}

        //미국주식 감시목록 삭제
        private void button6_Del_Click(object sender, EventArgs e)
        {
            Console.WriteLine("현재줄 {0} 선택 {1} {2}", dataGridView4.CurrentRow.Index,
                dataGridView4.SelectedRows[0].Index, 
                dataGridView4.RowCount);

            int row = dataGridView4.SelectedRows[0].Index;
            if (row > dataGridView4.RowCount - 2) return;

            string code = dataGridView4[1, row].Value.ToString();


            foreach (WatchStock st in watchUsStocks)
            {
                if (st.Equals(code))
                {
                    watchUsStocks.Remove(st);
                    break;
                }
            }

            dataGridView4.Rows.RemoveAt(row);

        }

        //국내 감시종목 삭제
        private void button7_Del_Click(object sender, EventArgs e)
        {
            int row = dataGridView3.SelectedRows[0].Index;
            if (row > dataGridView3.RowCount - 2) return;

            string code = dataGridView3[1, row].Value.ToString();

            foreach (WatchStock st in watchStocks)
            {
                if (st.Equals(code))
                {
                    watchStocks.Remove(st);
                    break;
                }
            }

            dataGridView3.Rows.RemoveAt(row);

        }

        //미국주식 감시시작
        private void button4_Click(object sender, EventArgs e)
        {
            string str = button4.Text;
            if(!isRealPriceRun)
            {
                button4.Text = "감시중지";
                StartWatchUsStock();
                isRealPriceRun = true;
            }
            else
            {
                isRealPriceRun = false;
                button4.Text = "감시적용";
                axITGExpertCtl4.UnRequestAllRealData();
                Thread.Sleep(15);
                axITGExpertCtl2.UnRequestAllRealData();
                Thread.Sleep(15);
                axITGExpertCtl1.UnRequestAllRealData();
                Thread.Sleep(15);
                axITGExpertCtl3.UnRequestAllRealData();
                Thread.Sleep(15);
                axITGExpertCtl5.UnRequestAllRealData();
            }
            
        }

        

        public void StartWatchUsStock()
        {

            Console.WriteLine("Start Watch us Stock");

            //목록만들기
            string requestStocks = "";

            //field size는 16이어야함_자리수가 부족하면 스페이스바로 채움
            string stock = "";

            foreach (WatchStock item in watchUsStocks)
            {
                stock = "D" + item.name + item.code;
                while (stock.Length < 16)
                {
                    stock += " ";
                }
                requestStocks += stock;
            }

            Console.WriteLine(requestStocks + " " + requestStocks.Length);

            //실시간시세조회요청을 보낸다
            //axITGExpertCtl4.RequestRealData("OS_STCNT0_R", "DNASTQQQ        DNASAAPL        ");
            axITGExpertCtl4.RequestRealData("OS_STCNT0_R", requestStocks);
            Console.WriteLine("Realdata request");
        }


        public void StartWatchStock()
        {

            Console.WriteLine("국내주식 실시간 시세정보 조회요청");

            //목록만들기
            string requestStocks = "";

            //field size는 9이어야함_자리수가 부족하면 스페이스바로 채움
            
            foreach (WatchStock item in watchStocks)
            {
                requestStocks += item.code + "   ";
            }

            

            //실시간시세조회요청을 보낸다
            //axITGExpertCtl4.RequestRealData("OS_STCNT0_R", requestStocks);
            requestManager.RegistTask(new Task( ()=> {
                axITGExpertCtl5.RequestRealData("SC_R", requestStocks);
            }));

            Console.WriteLine(requestStocks + " " + requestStocks.Length);
            Console.WriteLine("국내주식 실시간 시세정보 조회요청 완료하였습니다.");
        }


        private void button7_Click(object sender, EventArgs e)
        {
            string requestStocks = "";

            //field size는 9이어야함_자리수가 부족하면 스페이스바로 채움
            string stock = "";

            foreach (WatchStock item in watchStocks)
            {
                stock = item.code + "   ";
                requestStocks += stock;
            }
            axITGExpertCtl1.UnRequestAllRealData();
            Thread.Sleep(15);
            axITGExpertCtl2.UnRequestAllRealData();
            Thread.Sleep(15);
            axITGExpertCtl3.UnRequestAllRealData();
            Thread.Sleep(15);
            axITGExpertCtl4.UnRequestAllRealData();
            Thread.Sleep(15);
            axITGExpertCtl5.UnRequestAllRealData();

            Console.WriteLine("국내주식 실시간 시세정보 조회요청을 해제하였습니다.");
        }


        //국내주식 실시간시세를 받아 처리한다
        private void axITGExpertCtl4_ReceiveRealData(object sender, EventArgs e)
        {
            Console.WriteLine("Expert_Real4_Received : {0}개 Thread ID:{1}",
                axITGExpertCtl4.GetSingleFieldCount(), Thread.CurrentThread.ManagedThreadId);

            Console.Write("{0} {1} {2} {3} {4}\n", axITGExpertCtl4.GetSingleData(0, 0),
                axITGExpertCtl4.GetSingleData(1, 0), axITGExpertCtl4.GetSingleData(7, 0),
                axITGExpertCtl4.GetSingleData(11, 0), axITGExpertCtl4.GetSingleData(14, 0));

            int price, price_start, price_high, price_low, volume;
            float rate;
            string code;

            int i = 0;

            code = (string)axITGExpertCtl4.GetSingleData(0, 0);  //코드
            price = int.Parse((string)axITGExpertCtl4.GetSingleData(2, 0));
            price_start = int.Parse((string)axITGExpertCtl4.GetSingleData(7, 0));
            price_high = int.Parse((string)axITGExpertCtl4.GetSingleData(8, 0));
            price_low = int.Parse((string)axITGExpertCtl4.GetSingleData(9, 0));
            rate = float.Parse((string)axITGExpertCtl4.GetSingleData(5, 0));
            volume = int.Parse((string)axITGExpertCtl4.GetSingleData(13, 0));

            Console.WriteLine($"{code}, {price}, {rate}, {volume}");
            //watchStock list 갱신
            int lines = watchStocks.Count;
            WatchStock stock = null;

            for (i = 0; i<lines; i++)
            {
                stock = watchStocks[i];
                if(stock.code.Equals(code))
                {
                    stock.price0 = stock.price1;
                    stock.price1 = price;
                    if(stock.price1 != stock.price0)
                    {
                        stock.isChanged = true;
                        break;
                    }

                    
                }
            }

            Console.WriteLine("Expert_Real4_완료함");
        }

        private void axITGExpertCtl4_ReceiveSysMessage(object sender, _DITGExpertCtlEvents_ReceiveSysMessageEvent e)
        {
            Console.WriteLine("Received : {0} ", e.ToString());
            Console.WriteLine("sender name SYSMSG {0} {1}",
                sender.Equals(axITGExpertCtl4), sender.GetHashCode());
        }

        //주식종합차트 KST03010100 : 일정기간 시세조회
        public void StockPrice240(string code)
        {
            axITGExpertCtl4.SetMultiBlockData(0, 0, 0, "J");
            axITGExpertCtl4.SetMultiBlockData(0, 0, 1, code);

            axITGExpertCtl4.SetMultiBlockData(1, 0, 0, "J");
            axITGExpertCtl4.SetMultiBlockData(1, 0, 1, code);
            axITGExpertCtl4.SetMultiBlockData(1, 0, 2, startDay);
            axITGExpertCtl4.SetMultiBlockData(1, 0, 3, endDay);
            axITGExpertCtl4.RequestData("KST03010100");
            isApiBusy[4] = true;
        }

        //국내주식 분석용 stockData 조회
        
        public async void UpdateStockData()
        {
            isStockDataUpdate = false;

            
            time0 = time1 = DateTime.Now.Ticks;

            foreach (WatchStock stock in watchStocks)
            {
                
                //주식종합차트 KST03010100 주가조회

                StockPrice240(stock.code);
                //requestManager.RegistTask(new Task(() => StockPrice240(stock.code)));
                //isApiBusy[4] = true;
                //time0 = DateTime.Now.Ticks;
                await WaitApi(4);
                //time1 = DateTime.Now.Ticks;
                //dt = (time1 - time0) / 10000;
                //Console.WriteLine("fillstockdata_end Thread ID:{0} wait:{1}",
                //                    Thread.CurrentThread.ManagedThreadId, dt);

            }

            isStockDataUpdate = true;
            time1 = DateTime.Now.Ticks;
            dt = (time1 - time0) / 10000;
            Console.WriteLine("UPDATE STOCKDATA_end Thread ID:{0} wait:{1}, 개수 {2}",
                                Thread.CurrentThread.ManagedThreadId, dt, watchStocks.Count);
            
        }

        public async Task WaitApi(int apiNum, int delayTime = 50)
        {
            
            while (isApiBusy[apiNum] && isRunning)
            {
                await Task.Delay(delayTime);
            }
            
        }

        //국내주식 분석용 240일 주가자료를 구축한다 (종합주가차트조회)
        private void axITGExpertCtl4_ReceiveData(object sender, EventArgs e)
        {
            isApiBusy[4] = true;
            //Console.WriteLine("Expert4_Received : {0}개 Thread ID:{1}",
            //    axITGExpertCtl4.GetSingleFieldCount(), Thread.CurrentThread.ManagedThreadId);

            StockData stockData = new StockData();
            string strTemp;
            float fGap;

            //Util.PrintReceiveData(axITGExpertCtl4);
            stockData.stockName = axITGExpertCtl4.GetMultiData(0, 0, 6, 0).ToString().Trim();
            stockData.stockCode = axITGExpertCtl4.GetMultiData(0, 0, 8, 0).ToString().Trim();

            int nRecordCount = axITGExpertCtl4.GetMultiRecordCount(1);

            if (nRecordCount > 240) nRecordCount = 240;

            for (short i = 0; i < nRecordCount; i++)
            {
                //UnicodeToChar(m_ctlDaysPrice.GetMultiData(1, i, 0, 0), szTmp);
                strTemp = axITGExpertCtl4.GetMultiData(1, i, 0, 0).ToString();
                if (strTemp.Length == 0) break;

                stockData.stockPrice[i].date = strTemp;
                stockData.stockPrice[i].price = uint.Parse(axITGExpertCtl4.GetMultiData(1, i, 1, 0).ToString()); //종가
                stockData.stockPrice[i].price_start = uint.Parse(axITGExpertCtl4.GetMultiData(1, i, 2, 0).ToString()); //시가
                stockData.stockPrice[i].price_high = uint.Parse(axITGExpertCtl4.GetMultiData(1, i, 3, 0).ToString()); //고가
                stockData.stockPrice[i].price_low = uint.Parse(axITGExpertCtl4.GetMultiData(1, i, 4, 0).ToString()); //저가
                stockData.stockPrice[i].volume = uint.Parse(axITGExpertCtl4.GetMultiData(1, i, 5, 0).ToString());
                fGap = float.Parse(axITGExpertCtl4.GetMultiData(1, i, 11, 0).ToString());
                stockData.stockPrice[i].rate = (fGap * 100) / ((float)stockData.stockPrice[i].price - fGap);

            }
            stockDatas.Add(stockData);
            updatedStockData.Enqueue(stockDatas.Count - 1);

            //Console.Write($"{axITGExpertCtl4.GetMultiData(0, 0, 6, 0), 10}: ");
            //Console.WriteLine("Expert4_처리가 완료되었습니다 Thread ID:{0}",
            //    Thread.CurrentThread.ManagedThreadId);
            isApiBusy[4] = false;
        }


        


        

        
        private void button6_Click(object sender, EventArgs e)
        {
            Console.WriteLine("실시간조회 중지");
            axITGExpertCtl2.UnRequestAllRealData();
            axITGExpertCtl4.UnRequestAllRealData();
            axITGExpertCtl5.UnRequestAllRealData();
            //MakeStockList();


        }

        //주식차트 분봉조회 PST01010300 : 1분봉 : second "60"
        public void LookForMinutePrice(string code, string second)
        {
            axITGExpertCtl5.SetSingleData(0, "J");
            axITGExpertCtl5.SetSingleData(1, code);
            axITGExpertCtl5.SetSingleData(2, second);
            axITGExpertCtl5.SetSingleData(3, "Y");
            axITGExpertCtl5.RequestData("PST01010300");
            isApiBusy[5] = true;
        }

        //1분봉자료를 90봉 조회하여 stockData에 저장

        public async Task UpdateMinuteChartDate(StockData minStock)
        {
            //검색리스트 code -> 검색후 대기 2번 더 조회
            searchTimes = 0;
            //Console.WriteLine("UpdateMinChart 조회시작 {0}", minStock.stockName);
            LookForMinutePrice(minStock.stockCode, "60");
            //time0 = DateTime.Now.Ticks;

            await WaitApi(5, 10);
            
            //time1 = DateTime.Now.Ticks;
            //dt = (time1 - time0) / 10000;
            //Console.WriteLine("UpdateMinChart Thread ID:{0} wait:{1}",
            //                    Thread.CurrentThread.ManagedThreadId, dt);

            while (axITGExpertCtl5.IsMoreNextData() > 0 && searchTimes < 5)
            {
                searchTimes++;
                //Console.WriteLine("more" + searchTimes + " more :" + axITGExpertCtl5.IsMoreNextData());

                axITGExpertCtl5.RequestNextData("PST01010300");
                isApiBusy[5] = true;
                
                time0 = DateTime.Now.Ticks;
                await WaitApi(5, 10);
                time1 = DateTime.Now.Ticks;
                dt = (time1 - time0) / 10000;
                //Console.WriteLine("fillstockdata_end Thread ID:{0} wait:{1}",
                //                    Thread.CurrentThread.ManagedThreadId, dt);
            }

            //ma, macd 자료생성
            minStock.Ma_day_Price(5, Method.Ma_Short);
            minStock.Ma_day_Price(20, Method.Ma_Middle);
            minStock.Macd_Price(12, 26, 9);

            //출력해보기
            //Util.printStockData(minuteChartStock);
        }

        //다시조회; 분봉체크면 분봉, 아님 일봉
        private async void button9_Click(object sender, EventArgs e)
        {
            if(checkMinCandle.Checked)
            {
                for (int i = 0; i < 10; i++)
                {
                    WatchStock stock = watchStocks[i];
                    LookForMinutePrice(stock.code, "60");
                    Console.WriteLine("분봉조회요청 {0}", stock.code);
                    await WaitApi(5);
                }



            }
            else
            {
                //일봉조회 SCP
                if(scpThread == null)
                {
                    UpdateCurrentPriceThread();

                }


            }

            
        }

        public void UpdateCurrentPriceThread()
        {
            isScpRunning = true;
            scpThread = new Thread(async () => {

                time0 = time1 = DateTime.Now.Ticks;

                try
                {
                    while (isScpRunning)
                    {
                        int count = watchStocks.Count;
                        time0 = DateTime.Now.Ticks;
                        for (int i = 0; i < count; i++)
                        {
                            WatchStock stock = watchStocks[i];
                            checkCode = stock.code;
                            checkIndex = i;
                            //현재가 조회
                            LookForNowPrice(stock.code);
                            //requestManager.RegistTask(new Task(() => LookForNowPrice(stock.code)));
                            await WaitApi(6, 10);
                            Thread.Sleep(2);                            
                        }

                        time1 = DateTime.Now.Ticks;
                        dt = (time1 - time0) / 10000;
                        Thread.Sleep(50);
                        //Console.WriteLine("현재가조회(scp) thread ID:{0} wait:{1} 개수{2}",
                        //                    Thread.CurrentThread.ManagedThreadId, dt, count);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("현재가조회 Thread중지되었습니다. " + ex.Message);
                    isScpRunning = false;
                    scpThread = null;
                }

                scpThread = null;

            });

            scpThread.Start();

        }

        private void axITGExpertCtl5_ReceiveData(object sender, EventArgs e)
        {
            isApiBusy[5] = true;
            //Console.WriteLine("Expert5_Received_분봉 : {0}개 Thread ID:{1}",
            //    axITGExpertCtl5.GetSingleFieldCount(), Thread.CurrentThread.ManagedThreadId);

            //Util.PrintReceiveData(axITGExpertCtl5);

            int count = axITGExpertCtl5.GetMultiRecordCount(0);
            int sRow = searchTimes * 30;

            for(short i=0; i<count; i++)
            {
                minuteChartStock.stockPrice[sRow + i].date = axITGExpertCtl5.GetMultiData(0, i, 1, 0).ToString();
                minuteChartStock.stockPrice[sRow + i].price = uint.Parse(axITGExpertCtl5.GetMultiData(0, i, 2, 0).ToString());
                minuteChartStock.stockPrice[sRow + i].price_start = uint.Parse(axITGExpertCtl5.GetMultiData(0, i, 3, 0).ToString());
                minuteChartStock.stockPrice[sRow + i].price_high = uint.Parse(axITGExpertCtl5.GetMultiData(0, i, 4, 0).ToString());
                minuteChartStock.stockPrice[sRow + i].price_low = uint.Parse(axITGExpertCtl5.GetMultiData(0, i, 5, 0).ToString());
            }

            //Console.WriteLine("Expert5_분봉처리완료함");
            isApiBusy[5] = false;
        }

        //그리드1 현재보유주식 일자별 종가 그리드5에 표시
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int index = dataGridView1.CurrentCell.RowIndex;

            string gridCode = dataGridView1[1, index].Value.ToString();
            if (index == dataGridView1.Rows.Count - 1 || stockDatas.Count == 0) return;

            for (int i = 0; i < stockDatas.Count; i++)
            {
                if (stockDatas[i].stockCode.Equals(gridCode))
                {
                    index = i;
                    break;
                }
            }

            StockData stockData = stockDatas[index];

            string name = stockData.stockName;
            string code = stockData.stockCode;

            object[] rowData;

            dataGridView5.Rows.Clear();

            int maxLine = stockData.stockPrice.Length;
            for (int i = 0; i < maxLine; i++)
            {
                rowData = stockData.MakeRow(i);

                if (int.Parse(rowData[3].ToString()) == 0)
                    break;

                rowData[0] = i + 1;
                dataGridView5.Rows.Add(rowData);

            }

        }

        //조건에 해당하는 주식list Grid8에 표시
        private void button10_Click(object sender, EventArgs e)
        {
            int dayMa = int.Parse(txtDayMa.Text);
            int dayMacd = int.Parse(txtDayMacd.Text);
            int candle = int.Parse(txtCandle.Text);
            float rate = float.Parse(txtRate.Text);
            float rateMax = float.Parse(txtRateMax.Text);

            string condition = "";

            //조건체크가 모두 안되어 있으면 검색하지 않는다
            if ((checkMaGold.Checked || checkMaDead.Checked ||
                checkMacdGold.Checked || checkMacdDead.Checked ||
                checkPlus.Checked || checkMinus.Checked || checkRate.Checked) 
                == false)
            {
                return;
            }
           

            Console.WriteLine($"종목검색중 {stockDatas.Count}개 " );
            //검색조건은 And 조합임

            findCodes.Clear();
            findNames.Clear();
            dataGridView8.Rows.Clear();

            foreach (StockData stock in stockDatas)
            {
                condition = "";
                
                //day기준 골든크로스 및 macd (-) 인 경우
                if (checkMaGold.Checked)
                {
                    if (!stock.Ma_GoldenCross(dayMa))
                    {
                        continue;
                    }
                    condition += "Ma_Gold ";
                    
                }
                if (checkMaDead.Checked)
                {
                    if (!stock.Ma_DeadCross(dayMa))
                    {
                        continue;
                    }
                    condition += "Ma_Dead ";
                    
                }
                if(checkMacdGold.Checked)
                {
                    if (!stock.Macd_GoldenCross(dayMacd))
                    {
                        continue;
                    }
                    condition += "Macd_Gold ";
                    
                }
                if (checkMacdDead.Checked) {
                    
                    if (!stock.Macd_DeadCross(dayMacd))
                    {
                        continue;
                    }
                    condition += "Macd_Dead ";
                    
                }
                if (checkPlus.Checked)
                {
                    
                    if (!stock.Macd_Plus(dayMacd))
                    {
                        continue;
                    }
                    condition += "Macd_Plus ";
                    
                }
                if (checkMinus.Checked)
                {
                    
                    if (!stock.Macd_Minus(dayMacd))
                    {
                        continue;
                    }
                    condition += "Macd_Minus ";
                    
                }
                if (checkRate.Checked)
                {
                    //Console.WriteLine("rate check");
                    if (stock.stockPrice[candle + 1].price == 0)
                    {
                        continue;
                    }

                    float riseRate = ((float)stock.stockPrice[0].price / (float)stock.stockPrice[candle+1].price - 1) * 100.0f;

                    if (riseRate < rate || riseRate > rateMax)
                    {
                        continue;
                    }
                    condition += rate.ToString() + "% ";
                    
                }


                //기존목록과 중복되는지 확인한다
                if (findCodes.Count>0)
                {   
                    foreach (string code in findCodes)
                    {
                        if(code.Equals(stock.stockCode))
                        {
                            continue;
                        }
                    }
                }
                Console.WriteLine($"{stock.stockName}..{condition}");

                //여기까지 통과했으면 추가시켜야 한다
                findCodes.Add(stock.stockCode);
                findNames.Add(stock.stockName);
                string[] row = { stock.stockName, stock.stockCode, condition};
                dataGridView8.Rows.Add(row);

               
            }
            Console.WriteLine($"종목검색 완료 " + findCodes.Count + "개");
        }

        private void checkChart_CheckedChanged(object sender, EventArgs e)
        {
            if(checkChart.Checked)
            {
                chart1.Visible = true;
                //dataGridView7.Visible = false;
            }
            else
            {
                //dataGridView7.Visible = true;
                chart1.Visible = false;
            }
        }


        //차트설정
        public void InitiChart()
        {
            chart1.ChartAreas["ChartArea1"].AxisX.MajorGrid.LineWidth = 0;
            chart1.ChartAreas["ChartArea1"].AxisY.MajorGrid.LineWidth = 0;

            chart1.ChartAreas["ChartArea2"].AxisX.MajorGrid.LineWidth = 0;
            chart1.ChartAreas["ChartArea2"].AxisY.MajorGrid.LineWidth = 0;

            //시리즈0 : 캔들차트

            //chart1.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Candlestick;
            chart1.Series[0].XValueMember = "Time";
            chart1.Series[0].YValueMembers = "High,Low,Open,Close";
            chart1.Series[0].XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.String;
            chart1.Series[0].CustomProperties = "PriceDownColor=Blue,PriceUpColor=Red";
            //chart1.Series[0]["OpenCloseStyle"] = "Triangle";
            chart1.Series[0]["ShowOpenClose"] = "Both";
            chart1.DataManipulator.IsStartFromFirst = true;

            //시리즈1~5 : ma, macd
            //chart1.Series[1].BorderColor = Color.Black;
            //chart1.Series[1].BorderWidth = 3;
            //chart1.Series[2].BorderColor = Color.Orange;
            //chart1.Series[2].BorderWidth = 3;
            //chart1.Series[3].BorderColor = Color.Black;
            //chart1.Series[3].BorderWidth = 3;


        }

        void DrawChart(StockData stock, int viewmode = 20)
        {
            //chart1.Series[0].Points.AddXY("20200101", new object[] { 9, 3.0, 6, 9 });
            //chart1.Series[0].Points.AddXY(DateTime.Now.ToString("YYYYMMDD"), new object[] { 20, 10, 15, 16 });
            //chart1.Series[0].Points.AddXY("20220501", new object[] { 20, 10, 19, 11 });
            int count = viewmode;

            for (int i=0; i<chart1.Series.Count; i++)
            {
                chart1.Series[i].Points.Clear();
            }

            chart1.Series[0].Name = stock.stockName;

            int nCount = 0;
            uint priceMax = stock.stockPrice[0].price_high;
            uint priceMin = stock.stockPrice[0].price_low;
            string date;

            for(int i= 0; i < count; i++)
            //foreach (STOCKPRICE price in stock.stockPrice)
            {
                STOCKPRICE price = stock.stockPrice[i];

                if (price.price == 0)
                {
                    break;
                }
                else
                {
                    if (priceMax < price.price_high)
                        priceMax = price.price_high;

                    if (priceMin > price.price_low)
                        priceMin = price.price_low;
                }
                nCount++;
            }

            //Console.WriteLine($"최대값 : {priceMax}, 최소값: {priceMin}");

            if(stock.stockPrice[0].date.Length == 8)
            {
                chart1.ChartAreas[0].AxisY.Minimum = priceMin * 0.9;
                chart1.ChartAreas[0].AxisY.Maximum = priceMax * 1.1;

            }
            else
            {
                chart1.ChartAreas[0].AxisY.Minimum = priceMin * 0.998;
                chart1.ChartAreas[0].AxisY.Maximum = priceMax * 1.002;
            }

            //if (nCount > 120) nCount = 120;

            for (int i = nCount-1; i >=0; i--)
            {
                STOCKPRICE price = stock.stockPrice[i];
                object[] data = new object[] {price.price_high, price.price_low, price.price_start, price.price };

                date = (price.date.Length == 8) ? price.date.Substring(4, 4) :
                    price.date.Substring(0, 4);
                
                chart1.Series[0].Points.AddXY(date, data);
                chart1.Series[1].Points.AddXY(date, price.ma5);

                if (price.ma20 > 0)
                    chart1.Series[2].Points.AddXY(date, price.ma20);
                else
                    chart1.Series[2].Points.AddXY(date, " ");

                if (price.ma60 > 0)
                    chart1.Series[3].Points.AddXY(date, price.ma60);
                else
                    chart1.Series[3].Points.AddXY(date, " ");
                
                //if(i < nCount-26)
                if(price.macd == 0 && price.macd_signal == 0)
                {
                    chart1.Series[4].Points.AddXY(date, " ");
                    chart1.Series[5].Points.AddXY(date, " ");
                }
                else
                {
                    chart1.Series[4].Points.AddXY(date, price.macd);
                    chart1.Series[5].Points.AddXY(date, price.macd_signal);
                }
                //if(i < nCount - 26)
            }

        }

        

        private void chart1_Click(object sender, EventArgs e)
        {
            StockData stock = (checkMinCandle.Checked) ? minuteChartStock :stockDatas[drawChartStockIndex];
            Console.WriteLine("chart click");

            //확대하기
            if(viewMode == 180)
            {
                viewMode = 20;
                DrawChart(stock, 20);
                
                Console.WriteLine("chart click1");
            }

            else if(viewMode == 20)  //축소하기
            {
                viewMode = 60;
                DrawChart(stock, 60);

                Console.WriteLine("chart click2");
            }

            else if (viewMode == 60)  //축소하기
            {
                viewMode = 120;
                DrawChart(stock, 120);
                Console.WriteLine("chart click3");
            }

            else if (viewMode == 120)  //축소하기
            {
                viewMode = 180;
                DrawChart(stock, 180);
                Console.WriteLine("chart click0");
            }
        }

        private void dataGridView8_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int index = dataGridView8.CurrentCell.RowIndex;
            bool isChart = checkChart.Checked;

            string gridCode = dataGridView8[1, index].Value.ToString();
            if (index == dataGridView8.Rows.Count - 1 || stockDatas.Count == 0) return;

            for (int i = 0; i < stockDatas.Count; i++)
            {
                if (stockDatas[i].stockCode.Equals(gridCode))
                {
                    index = i;
                    drawChartStockIndex = i;
                    break;
                }
            }

            StockData stockData = stockDatas[index];

            if (isChart)
            {
                //viewMode = 20;
                DrawChart(stockData, viewMode);
            }

            string name = stockData.stockName;
            string code = stockData.stockCode;

            object[] rowData;

            dataGridView7.Rows.Clear();

            int maxLine = stockData.stockPrice.Length;
            for (int i = 0; i < maxLine; i++)
            {
                rowData = stockData.MakeRow(i);

                if (int.Parse(rowData[3].ToString()) == 0)
                    break;

                rowData[0] = i + 1;
                dataGridView7.Rows.Add(rowData);

            }
        }

        private void button11_Trade_Click(object sender, EventArgs e)
        {
            //findList 대상으로 분봉조회하여 매수조건 매도조건부합 체크
            //30초단위로 검색 : 
            //1봉전 조건부합하면 종목, 시간, 금액, 매수/매도 log 기록
            if(!isStartTrade)
            {
                isStartTrade = true;
                button11_Trade.Text = "진행중";
                button11_Trade.BackColor = Color.LightGreen;
            }
            else
            {
                isStartTrade = false;
                button11_Trade.Text = "거래시작";
                button11_Trade.BackColor = Color.Moccasin;
            }

        }

        public async void StartTrade()
        {
            int count = findCodes.Count;
            string tradeLog = "";
            string printLog = "";
            bool isHold = false;

            for (int i=0; i<count; i++)
            {
                minuteChartStock = new StockData();
                minuteChartStock.stockCode = findCodes[i];
                minuteChartStock.stockName = findNames[i];
                await UpdateMinuteChartDate(minuteChartStock);

                //매수조건 부합하는지
                if(minuteChartStock.Ma_GoldenCross(1) || minuteChartStock.Ma_GoldenCross(2) || minuteChartStock.Ma_GoldenCross(3))
                {
                    isHold = false;

                    if (buyCodes.IndexOf(findCodes[i]) != -1)
                    {
                        isHold = true;
                        Console.WriteLine("이미보유중 : {0}", findNames[i]);
                    }
                    
                    if(!isHold)
                    {
                        TradeStock trade = new TradeStock();
                        tradeLog = String.Format($"매수 : {findNames[i]},{findCodes[i]}," +
                        $"{minuteChartStock.stockPrice[0].date},{minuteChartStock.stockPrice[0].price}");

                        Console.WriteLine(tradeLog);
                        if(printLog.Length > 0)
                            printLog += "\n"+ tradeLog;
                        else
                            printLog += tradeLog;

                        trade.code = findCodes[i];
                        trade.name = findNames[i];
                        trade.buyDate = minuteChartStock.stockPrice[0].date;
                        trade.buyPrice = minuteChartStock.stockPrice[0].price;
                        tradeStocks.Add(trade);
                        buyCodes.Add(findCodes[i]);
                    }
                }

                //매도조건 부합하는지 : 1봉전 
                if (minuteChartStock.Ma_DeadCross(1) || minuteChartStock.Ma_DeadCross(2))
                {
                    //보유중이라면 매도
                    int buyIndex = -1;
                    if ((buyIndex = buyCodes.IndexOf(findCodes[i])) != -1)
                    {

                        tradeLog = String.Format($"매도 : {findNames[i]} {findCodes[i]} " +
                        $"{minuteChartStock.stockPrice[0].date} {minuteChartStock.stockPrice[0].price}\n");

                        Console.WriteLine(tradeLog);
                        if (printLog.Length > 0)
                            printLog += "\n" + tradeLog;
                        else
                            printLog += tradeLog;

                        //tradeStocks index 찾기
                        int j = 0;
                        for(j=0; j<tradeStocks.Count; j++)
                        {
                            if(tradeStocks[j].code == findCodes[i])
                            {
                                break;
                            }
                        }

                        tradeStocks[j].sellDate = minuteChartStock.stockPrice[0].date;
                        tradeStocks[j].sellPrice = minuteChartStock.stockPrice[0].price;
                        buyCodes.RemoveAt(buyIndex);
                    }
                    
                }

                //Console.WriteLine("{0} 2봉 {1} {2}, 1봉 {3} {4}, 0봉 {5} {6}, Gold {7} Dead {8}", minuteChartStock.stockName,
                //    minuteChartStock.stockPrice[2].ma5, minuteChartStock.stockPrice[2].ma20,
                //    minuteChartStock.stockPrice[1].ma5, minuteChartStock.stockPrice[1].ma20,
                //    minuteChartStock.stockPrice[0].ma5, minuteChartStock.stockPrice[0].ma20,
                //    minuteChartStock.Ma_GoldenCross(1), minuteChartStock.Ma_DeadCross(1));
            }

            if(printLog.Length > 0)
            {
                try
                {
                    StreamWriter file = File.AppendText("tradelog.txt");
                    file.WriteLine(printLog);
                    file.Close();
                }
                catch (IOException e)
                {
                    WriteLine("파일오류 : {0} ", e.Message);
                    return;
                }
            }
            

        }

        private async void checkMinCandle_CheckedChanged(object sender, EventArgs e)
        {

            StockData stockData = stockDatas[drawChartStockIndex];

            //분봉체크한 경우, 차트를 분봉으로 변경
            if (checkMinCandle.Checked)
            {
                minuteChartStock = new StockData();
                minuteChartStock.stockCode = stockData.stockCode;
                minuteChartStock.stockName = stockData.stockName;

                await UpdateMinuteChartDate(minuteChartStock);

                DrawChart(minuteChartStock, viewMode);

                string name = minuteChartStock.stockName;
                string code = minuteChartStock.stockCode;

                object[] rowData;

                dataGridView7.Rows.Clear();

                int maxLine = minuteChartStock.stockPrice.Length;
                for (int i = 0; i < maxLine; i++)
                {
                    rowData = minuteChartStock.MakeRow(i);
                    
                    if (rowData[3] == null)
                        break;

                    rowData[0] = i + 1;
                    dataGridView7.Rows.Add(rowData);

                }
            }
            else
            {
                DrawChart(stockData, viewMode);
                

                string name = stockData.stockName;
                string code = stockData.stockCode;

                object[] rowData;

                dataGridView7.Rows.Clear();

                int maxLine = stockData.stockPrice.Length;
                for (int i = 0; i < maxLine; i++)
                {
                    rowData = stockData.MakeRow(i);

                    if (int.Parse(rowData[3].ToString()) == 0)
                        break;

                    rowData[0] = i + 1;
                    dataGridView7.Rows.Add(rowData);

                }
            }
        }


        //watchStock 목록 추가하기 Grid6
        private async void button8_InSert_Click(object sender, EventArgs e)
        {
            bool isExist = false;

            string[] items = txtStockList.Text.Split('\t');

            //dataGridView에 있는 종목이 있는 경우면
            if (items.Length < 2)
            {
                WriteLine("올바른 항목을 선택하세요");
                txtStockList.Text = "";
                return;
            }


            //그리드상 중복여부  체크
            int nCount = dataGridView6.RowCount - 1;
            for (int i = 0; i < nCount; i++)
            {
                if (items[1].Equals(dataGridView6[1, i].Value.ToString()))
                {
                    isExist = true;
                    WriteLine("목록에 이미 있습니다");
                    break;
                }
            }

            if (items[1].Length > 0 && !isExist)
            {
                WatchStock stock = new WatchStock(items[0].Trim(), items[1].Trim());

                dataGridView6.Rows.Add(stock.MakeRowStock());
                watchStocks.Add(stock);
                WriteLine("목록에 추가하였습니다");

                DisplayWatchStock(dataGridView3, watchStocks);
                DisplayWatchStock(dataGridView6, watchStocks);
                
                //추가종목 240일 상세내역 구축하기
                StockPrice240(stock.code);
                await WaitApi(4);

                //isStockDataUpdate = true;

            }
            txtStockList.Text = "";

        }

        private void dataGridView6_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            DataGridView dataGridView = (DataGridView)sender;
            int index = e.Column.Index;

            //DataGridViewColumn currentColumn = dataGridView.Columns[e.Column.Index];
            if (index == 2)
            {
                float num1 = float.Parse(e.CellValue1.ToString());
                float num2 = float.Parse(e.CellValue2.ToString());

                e.SortResult = (num2 > num1) ? 1 : ((num2 < num1) ? -1 : 0);
            }
            else
            {
                e.SortResult = string.Compare(
                    e.CellValue1.ToString(), e.CellValue2.ToString());
            }

            e.Handled = true;

        }

        //해외주식 일자별 종가조회 OS_ST03
        private void axITGExpertCtl7_ReceiveData(object sender, EventArgs e)
        {

        }

        private void axITGExpertCtl7_ReceiveRealData(object sender, EventArgs e)
        {

        }



        //국내현재가 SCP 회신
        private void axITGExpertCtl6_ReceiveData(object sender, EventArgs e)
        {
            isApiBusy[6] = true;
            //Console.WriteLine("Expert6_Received_현재가 : {0}개 Thread ID:{1}",
            //   axITGExpertCtl6.GetSingleFieldCount(), Thread.CurrentThread.ManagedThreadId);

            //Util.PrintReceiveData(axITGExpertCtl6);
            int price, price_start, price_high, price_low;
            float rate;

            price = int.Parse(axITGExpertCtl6.GetSingleData(11, 0).ToString());
            price_start = int.Parse(axITGExpertCtl6.GetSingleData(18, 0).ToString());
            price_high = int.Parse(axITGExpertCtl6.GetSingleData(19, 0).ToString());
            price_low = int.Parse(axITGExpertCtl6.GetSingleData(20, 0).ToString());
            rate = float.Parse(axITGExpertCtl6.GetSingleData(14, 0).ToString());

            WatchStock stock = watchStocks[checkIndex];
            stock.price0 = stock.price1;
            stock.price1 = price;
            stock.rate = rate;
            if (stock.price0 != price)
                stock.isChanged = true;

            StockData stockData = stockDatas[checkIndex];
            stockData.stockPrice[0].price = (uint)price;
            stockData.stockPrice[0].price_start = (uint)price_start;
            stockData.stockPrice[0].price_high = (uint)price_high;
            stockData.stockPrice[0].price_low = (uint)price_low;
            stockData.stockPrice[0].rate = rate;

            //StockData stockData = stockDatas[checkIndex];
            //stockData.Macd0_Update(12, 26, 9);
            //stockData.Ma0_Update();

            //Console.WriteLine("변경후 {0} Expert6 현재가 {1}, {2}, 조회가 : {3}", stockDatas[checkIndex].stockName, stockDatas[checkIndex].stockPrice[0].price, watchStocks[checkIndex].price1, price);
            //Console.WriteLine("{0} Expert6 현재가조회 완료 id : {1}", stockDatas[checkIndex].stockName, Thread.CurrentThread.ManagedThreadId);
            updatedWatchStockPrice.Enqueue(checkIndex);
            isApiBusy[6] = false;
        }

        private void axITGExpertCtl6_ReceiveRealData(object sender, EventArgs e)
        {

        }


        //클릭하면 gridview5에 일자별 현재가 정보 출력
        private void dataGridView3_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int index = dataGridView3.CurrentCell.RowIndex;

            string grid3Code = dataGridView3[1, index].Value.ToString();
            if (index == dataGridView3.Rows.Count - 1 || stockDatas.Count == 0) return;

            for (int i=0; i<stockDatas.Count; i++)
            {
                if (stockDatas[i].stockCode.Equals(grid3Code))
                {
                    index = i;
                    break;
                }
            }

            StockData stockData = stockDatas[index];

            string name = stockData.stockName;
            string code = stockData.stockCode;

            object[] rowData;

            dataGridView5.Rows.Clear();

            int maxLine = stockData.stockPrice.Length;
            for(int i=0; i < maxLine; i++)
            {
                rowData = stockData.MakeRow(i);
                
                if (int.Parse(rowData[3].ToString()) == 0)
                    break;

                rowData[0] = i + 1;
                dataGridView5.Rows.Add(rowData);

            }


        }

        //클릭하면 gridview7에 일자별 현재가 정보 출력
        private async void dataGridView6_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView dataGridView6 = (DataGridView)sender;

            int index = dataGridView6.CurrentCell.RowIndex;
            bool isChart = checkChart.Checked;
            bool isMin = checkMinCandle.Checked;

            string grid6Code = dataGridView6[1, index].Value.ToString();
            if (index == dataGridView6.Rows.Count - 1 || stockDatas.Count == 0) return;

            for (int i = 0; i < stockDatas.Count; i++)
            {
                if (stockDatas[i].stockCode.Equals(grid6Code))
                {
                    index = i;
                    break;
                }
            }

            StockData stockData = stockDatas[index];

            //분봉체크가 없는 경우
            if(!isMin)
            {
                drawChartStockIndex = index;

                if (isChart)
                {
                    //viewMode = 20;
                    DrawChart(stockData, viewMode);
                }

                string name = stockData.stockName;
                string code = stockData.stockCode;

                object[] rowData;

                dataGridView7.Rows.Clear();

                int maxLine = stockData.stockPrice.Length;
                for (int i = 0; i < maxLine; i++)
                {
                    rowData = stockData.MakeRow(i);

                    if (int.Parse(rowData[3].ToString()) == 0)
                        break;

                    rowData[0] = i + 1;
                    dataGridView7.Rows.Add(rowData);

                }
            }
            else //분봉체크된 경우
            {
                minuteChartStock = new StockData();
                minuteChartStock.stockCode = stockData.stockCode;
                minuteChartStock.stockName = stockData.stockName;

                await UpdateMinuteChartDate(minuteChartStock);

                drawChartStockIndex = index;

                //if (isChart)
                {
                    //viewMode = 60;
                    DrawChart(minuteChartStock, viewMode);
                }

                string name = minuteChartStock.stockName;
                string code = minuteChartStock.stockCode;

                object[] rowData;

                dataGridView7.Rows.Clear();

                int maxLine = minuteChartStock.stockPrice.Length;
                for (int i = 0; i < maxLine; i++)
                {
                    rowData = minuteChartStock.MakeRow(i);
                    //Console.WriteLine("{0}, {1}, {2}, Length:{3}", i, rowData[0], rowData[3]);

                    if (rowData[3] == null)
                        break;

                    rowData[0] = i + 1;
                    dataGridView7.Rows.Add(rowData);

                }
            }
            

            //Console.WriteLine("grid6클릭 {0} {1}", stockDatas[index].stockName, stockDatas[index].stockPrice[0].price);
        }

        private void axITGExpertCtl5_ReceiveRealData(object sender, EventArgs e)
        {
            //Console.WriteLine("Expert_Real5_Received : {0}개 Thread ID:{1}",
            //    axITGExpertCtl5.GetSingleFieldCount(), Thread.CurrentThread.ManagedThreadId);

            int price, price_start, price_high, price_low, volume;
            float rate;
            string code;

            int i = 0;

            code = (string)axITGExpertCtl5.GetSingleData(0, 0);  //코드
            price = int.Parse((string)axITGExpertCtl5.GetSingleData(2, 0));
            //price_start = int.Parse((string)axITGExpertCtl5.GetSingleData(7, 0));
            //price_high = int.Parse((string)axITGExpertCtl5.GetSingleData(8, 0));
            //price_low = int.Parse((string)axITGExpertCtl5.GetSingleData(9, 0));
            rate = float.Parse((string)axITGExpertCtl5.GetSingleData(5, 0));
            //volume = int.Parse((string)axITGExpertCtl5.GetSingleData(13, 0));

            //Console.WriteLine($"{code}, {price}, {rate}, {volume}");
            //watchStock list 갱신
            int lines = watchStocks.Count;
            WatchStock stock = null;

            for (i = 0; i < lines; i++)
            {
                stock = watchStocks[i];
                if (stock.code.Equals(code))
                {
                    stock.price0 = stock.price1;
                    stock.price1 = price;
                    stock.rate = rate;
                    if (stock.price1 != stock.price0)
                    {
                        stock.isChanged = true;
                        updatedWatchStockPrice.Enqueue(i);
                    }

                    break;
                }
            }
            //updatedWatchStockPrice.Enqueue(i);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            axITGExpertCtl1.UnRequestAllRealData();
            Thread.Sleep(20);
            axITGExpertCtl2.UnRequestAllRealData();
            Thread.Sleep(20);
            axITGExpertCtl3.UnRequestAllRealData();
            Thread.Sleep(20);
            axITGExpertCtl4.UnRequestAllRealData();
            Thread.Sleep(20);
            axITGExpertCtl5.UnRequestAllRealData();
            Thread.Sleep(20);


            isScpRunning = false;
            //scpThread.Interrupt();
            
            isRunning = false; //updateThread 종료
            //updateThread.Interrupt();
            requestManager.Stop();  //
            Console.WriteLine("실시간조회 해제요청 완료하였습니다");

            Util.PrintTradeStocks(tradeStocks);

        }

        


        //void Ma_day_Price(StockData stockData, int day, Method method)
        //{
        //    int i, nMaxcount = 240;
        //    uint sumPrice = 0;

        //    for (i = 0; i < day; i++)
        //    {
        //        if (stockData.stockPrice[i].price == 0) return;
        //        sumPrice += stockData.stockPrice[i].price;
        //    }

        //    for (i = 0; i < nMaxcount - day; i++)
        //    {
        //        if (stockData.stockPrice[i + day - 1].price == 0) break; //마지막이 0이면 멈춤
        //        if (i > 0)
        //        {
        //            sumPrice += stockData.stockPrice[i + day - 1].price - stockData.stockPrice[i - 1].price;
        //        }
        //        switch (method)
        //        {

        //            case Method.Ma_Short:
        //                stockData.stockPrice[i].ma5 = ((float)sumPrice / day);
        //                break;
        //            case Method.Ma_Middle:
        //                stockData.stockPrice[i].ma20 = ((float)sumPrice / day);
        //                break;
        //            case Method.Ma_Long:
        //                stockData.stockPrice[i].ma60 = ((float)sumPrice / day);
        //                break;
        //        }
        //    }
        //}

        //void Macd_Price(StockData stockData, int nMa12, int nMa26, int nSinal9)
        //{
        //    int i, nMaxcount = 240;
        //    int nRealCount;
        //    float fMacd_ma12, fMacd_ma26, fMacd;

        //    for (i = 0; i < nMaxcount; i++)
        //    {
        //        if (stockData.stockPrice[i].price == 0) break;
        //    }
        //    nRealCount = i - 1;

        //    if (nRealCount < 50) return;  //실제 자료개수가 50개 이하면 작업안함

        //    fMacd_ma12 = (float)stockData.stockPrice[nRealCount].price;
        //    fMacd_ma26 = (float)stockData.stockPrice[nRealCount].price;

        //    for (i = 1; i < nMa26; i++)
        //    {
        //        if (stockData.stockPrice[nRealCount - i].price == 0) return;
        //        fMacd_ma12 = stockData.stockPrice[nRealCount - i].price * (2.0f / (nMa12 + 1))
        //            + fMacd_ma12 * (1.0f - 2.0f / (nMa12 + 1));
        //        fMacd_ma26 = stockData.stockPrice[nRealCount - i].price * (2.0f / (nMa26 + 1))
        //            + fMacd_ma26 * (1.0f - 2.0f / (nMa26 + 1));
        //        //m_StockData.stockPrice[nRealCount - i].macd = fMacd_ma12 - fMacd_ma26;
        //    }
        //    stockData.stockPrice[nRealCount - i].macd12 = fMacd_ma12;
        //    stockData.stockPrice[nRealCount - i].macd26 = fMacd_ma26;
        //    stockData.stockPrice[nRealCount - i + 1].macd = fMacd_ma12 - fMacd_ma26;
        //    stockData.stockPrice[nRealCount - i + 1].macd_signal = fMacd_ma12 - fMacd_ma26;

        //    for (i = nMa26; i <= nRealCount; i++)
        //    {
        //        if (stockData.stockPrice[nRealCount - i].price == 0) break;

        //        fMacd_ma12 = stockData.stockPrice[nRealCount - i].price * (2.0f / (nMa12 + 1))
        //            + fMacd_ma12 * (1.0f - 2.0f / (nMa12 + 1));
        //        fMacd_ma26 = stockData.stockPrice[nRealCount - i].price * (2.0f / (nMa26 + 1))
        //            + fMacd_ma26 * (1.0f - 2.0f / (nMa26 + 1));

        //        stockData.stockPrice[nRealCount - i].macd12 = fMacd_ma12;
        //        stockData.stockPrice[nRealCount - i].macd26 = fMacd_ma26;

        //        fMacd = fMacd_ma12 - fMacd_ma26;
        //        stockData.stockPrice[nRealCount - i].macd = fMacd;

        //        stockData.stockPrice[nRealCount - i].macd_signal = fMacd * (2.0f / (nSinal9 + 1))
        //            + stockData.stockPrice[nRealCount - i + 1].macd_signal * (1.0f - 2.0f / (nSinal9 + 1));
        //    }

        //}


        //void StockRiseRate(StockData stockData)
        //{
        //    //m_cs.Lock();
            
        //    if (stockData.stockPrice[150].ma60 > 0)
        //        stockData.ma60_FarRate = (stockData.stockPrice[100].ma60 / stockData.stockPrice[150].ma60 - 1.0f) * 100;
        //    if (stockData.stockPrice[100].ma60 > 0)
        //        stockData.ma60_MidRate = (stockData.stockPrice[50].ma60 / stockData.stockPrice[100].ma60 - 1.0f) * 100;
        //    if (stockData.stockPrice[150].ma60 > 0)
        //        stockData.ma60_CurRate = (stockData.stockPrice[0].ma60 / stockData.stockPrice[50].ma60 - 1.0f) * 100;

        //    if (stockData.stockPrice[30].ma20 > 0)
        //        stockData.ma20_FarRate = (stockData.stockPrice[20].ma20 / stockData.stockPrice[30].ma20 - 1.0f) * 100;
        //    if (stockData.stockPrice[20].ma20 > 0)
        //        stockData.ma20_MidRate = (stockData.stockPrice[10].ma20 / stockData.stockPrice[20].ma20 - 1.0f) * 100;
        //    if (stockData.stockPrice[10].ma20 > 0)
        //        stockData.ma20_CurRate = (stockData.stockPrice[0].ma20 / stockData.stockPrice[10].ma20 - 1.0f) * 100;

        //    if (stockData.stockPrice[6].price > 0)
        //        stockData.day_FarRate = ((float)stockData.stockPrice[4].price / (float)stockData.stockPrice[6].price - 1.0f) * 100;
        //    if (stockData.stockPrice[4].price > 0)
        //        stockData.day_MidRate = ((float)stockData.stockPrice[2].price / (float)stockData.stockPrice[4].price - 1.0f) * 100;
        //    if (stockData.stockPrice[2].price > 0)
        //        stockData.day_CurRate = ((float)stockData.stockPrice[0].price / (float)stockData.stockPrice[2].price - 1.0f) * 100;

        //    //m_cs.Unlock();
        //}

        //public void PrintReceiveData(AxITGExpertCtl axITGExpertCtl1)
        //{
        //    int field = axITGExpertCtl1.GetMultiFieldCount(0, 0);
        //    int record = axITGExpertCtl1.GetMultiRecordCount(0);
        //    int block = axITGExpertCtl1.GetMultiBlockCount();

        //    string str = String.Format("BLOCK {0} RECORD {1} Field {2}", block, record, field);
        //    Console.WriteLine(str);
        //    for (short k = 0; k < block; k++)
        //    {
        //        record = axITGExpertCtl1.GetMultiRecordCount(k);
        //        for (short j = 0; j < record; j++)
        //        {
        //            field = axITGExpertCtl1.GetMultiFieldCount(k, j);
        //            for (short i = 0; i < field; i++)
        //            {
        //                Console.Write("{0},{1},{2}: {3} \n", k, j, i, axITGExpertCtl1.GetMultiData(k, j, i, 0));
        //            }
        //            Console.WriteLine();
        //        }
        //    }

        //    field = axITGExpertCtl1.GetSingleFieldCount();
        //    for (short i = 0; i < field; i++)
        //    {
        //        Console.Write("{0}: {1} \n", i, axITGExpertCtl1.GetSingleData(i, 0));
        //    }

        //}

    }
}   
