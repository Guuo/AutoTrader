using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Deedle;
using System.Collections;

namespace AutoTrader
{
    public partial class Form1 : Form
    {
        DataLoader dataLoader;
        LiveTrader liveTrader;
        Backtester backtester = new Backtester();
        List<IStrategy> strategyList = new List<IStrategy>();
        int listBoxSelectedIndex = -1;
        IStrategy selectedStrategy;
        public Form1(DataLoader dl)
        {
            dataLoader = dl;
            liveTrader = new LiveTrader(dl);
            InitializeComponent();
            Strategy.SuperTrendStochStrategy SuperTrendStochStrategy = new Strategy.SuperTrendStochStrategy();
            Strategy.SuperTrendStochStrategy2 SuperTrendStochStrategy2 = new Strategy.SuperTrendStochStrategy2();
            Strategy.SuperTrendStochStrategy3 SuperTrendStochStrategy3 = new Strategy.SuperTrendStochStrategy3();
            Strategy.TradeProStrategy TradeProStrategy = new Strategy.TradeProStrategy();
            Strategy.MarketCipherStrategy MarketCipherStrategy = new Strategy.MarketCipherStrategy();
            Strategy.ClassicMACDStrategy ClassicMACDStrategy = new Strategy.ClassicMACDStrategy();
            Strategy.ADXBBStrategy ADXBBStrategy = new Strategy.ADXBBStrategy();
            Strategy.RSIDivergenceStrategy RSIDivergenceStrategy = new Strategy.RSIDivergenceStrategy();

            strategyList.Add(SuperTrendStochStrategy);
            strategyList.Add(SuperTrendStochStrategy2);
            strategyList.Add(SuperTrendStochStrategy3);
            strategyList.Add(TradeProStrategy);
            strategyList.Add(MarketCipherStrategy);
            strategyList.Add(ClassicMACDStrategy);
            strategyList.Add(ADXBBStrategy);
            strategyList.Add(RSIDivergenceStrategy);
            StrategySelectionBox.DataSource = strategyList;
            StrategySelectionBox.DisplayMember = "stratName";
            

            StrategySelectionBox.SelectedIndexChanged +=
                new EventHandler(StrategySelectionBox_SelectedIndexChanged);
            
        }

        private void StrategySelectionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSelectedIndex == StrategySelectionBox.SelectedIndex)
                StrategySelectionBox.ClearSelected();

            listBoxSelectedIndex = StrategySelectionBox.SelectedIndex;
            if (StrategySelectionBox.SelectedIndex != -1)
            {
                GetDataCustomTimeRange.Enabled = true;
                selectedStrategy = strategyList[StrategySelectionBox.SelectedIndex];
                if(selectedStrategy.Timeframes.Count > 1)
                {
                    IntervalComboBox.Enabled = false; 
                }
                else
                {
                    IntervalComboBox.Enabled = true;
                }
            }
            else
            {
                GetDataCustomTimeRange.Enabled = false;
            }
        }

        private async void GetDataBtn_Click(object sender, EventArgs e)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            DateTime dateTimeFrom = DateTime.UtcNow.AddHours(-3000); //83h is under 5k -2928
            var parameters5Min = new DataRequestParameters(dateTimeFrom, dateTimeNow, 5, "8874"); //bitcoin 1057391
            var parameters15Min = new DataRequestParameters(dateTimeFrom, dateTimeNow, 15, "8874");
            var parameterList = new List<DataRequestParameters>();
            parameterList.Add(parameters5Min);
            parameterList.Add(parameters15Min);
            var task = dataLoader.GetPriceDataAsync(parameterList);
            var dataPackageList = await task;
            backtester.PriceDataPackage = dataPackageList;
            Console.WriteLine("Data fetched.");
            
        }
        private async void GetDataCustomTimeRange_Click(object sender, EventArgs e)
        {
            TimeSpan fromTime = DateTimePickerFromTime.Value.TimeOfDay;
            TimeSpan toTime = DateTimePickerToTime.Value.TimeOfDay;
            DateTime dateTimeFrom = DateTimePickerFrom.Value.Date.Add(fromTime); //83h is under 5k
            DateTime dateTimeTo = DateTimePickerTo.Value.Date.Add(toTime);
            dateTimeFrom = dateTimeFrom.ToUniversalTime();
            dateTimeTo = dateTimeTo.ToUniversalTime();

            var parameterList = new List<DataRequestParameters>();
            if (selectedStrategy.Timeframes.Count > 0)
            {
                foreach (string timeframe in selectedStrategy.Timeframes)
                {
                    var parameters = new DataRequestParameters(dateTimeFrom, dateTimeTo, int.Parse(timeframe), SymbolIDTextBox.Text);
                    parameterList.Add(parameters);
                }
            }/*
            else
            {
                int timeframe = int.Parse((string)IntervalComboBox.SelectedItem);
                var parameters = new DataRequestParameters(dateTimeFrom, dateTimeTo, timeframe, SymbolIDTextBox.Text);
                parameterList.Add(parameters);
            }*/
            

            //parameterList.Add(parameters1Min);
            var task = dataLoader.GetPriceDataAsync(parameterList, SaveToDiskCheckBox.Checked, FileNameTextBox.Text);
            var dataPackageList = await task;
            backtester.PriceDataPackage = dataPackageList;
            Console.WriteLine("Data fetched.");
        }

        private void CalculateButton_Click(object sender, EventArgs e)
        {
            /*
            Frame<int, string> data = Frame.ReadCsv("1Y_5MinData.csv");
            var daatta = data.IndexRows<DateTime>("Key");
            
            List<Frame<DateTime, string>> dataPackageList = new() { daatta };
            backtester.PriceDataPackage = dataPackageList;
            Strategy.CoppockCurveStrategy testStrategy = new();
            backtester.RunBacktest(backtester.PriceDataPackage, testStrategy);
            */
            if(LoadDataFromMemory.Checked == true)
            {
                runCipherStratOptimizer(backtester.PriceDataPackage);
            }
            else
                runCipherStratOptimizer();


        }
        
        private void runCoppockHullStratOptimizer(List<Frame<DateTime, string>> dataPackage = null)
        {
            Frame<DateTime, string> daatta = null;
            if (dataPackage == null)
            {
                Frame<int, string> data = Frame.ReadCsv("NeutralHold_1HData.csv");
                daatta = data.IndexRows<DateTime>("Key");
            }
            else
            {
                daatta = backtester.PriceDataPackage.First();
            }

            ConcurrentBag<List<IComparable>> statListBag = new();
            int testCounter = 0;
            Console.WriteLine("Parallel task about to begin");

            int from = 18;
            Parallel.For(from, 38,
                from =>
                {

                    Frame<DateTime, string> tempDataFrame = Frame.CreateEmpty<DateTime, string>();
                    Frame<DateTime, string> parallelDaatta = tempDataFrame.Merge(daatta);
                    List<Frame<DateTime, string>> dataPackageList = new() { parallelDaatta };
                    BacktesterOptimizer backOp = new BacktesterOptimizer();
                    backOp.PriceDataPackage = dataPackageList;
                    Strategy.CoppockHullStrategyOptimizable strat = new Strategy.CoppockHullStrategyOptimizable();
                    for (int i = 20; i < 43; i++)
                    {
                        for (int j = 15; j < 45; j++)
                        {
                            for (int k = 14; k < 44; k++)
                            {
                                if (j > k)
                                {
                                    int[] options = new int[] { from, i, j, k };
                                    testCounter++;
                                    try
                                    {
                                        //statListBag.Add(backOp.RunBacktest(backOp.PriceDataPackage, strat, options, testCounter));
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Error GET :D");
                                        Console.WriteLine(ex);


                                        Console.WriteLine("What wrong? :D");
                                    }
                                }

                            }
                        }
                    }

                });

            decimal bestAccValue = 0;
            int bestIndex = 0;
            List<List<IComparable>> statListList = new();
            foreach (List<IComparable> statList in statListBag)
            {
                statListList.Add(statList);
            }
            statListList.Sort((x, y) => x[7].CompareTo(y[7]));
            for (int i = 0; i < statListList.Count; i++)
            {
                if ((decimal)statListList[i][0] > bestAccValue)
                {
                    bestAccValue = (decimal)statListList[i][0];
                    bestIndex = i;
                }
            }

            Console.WriteLine("accountValue          " + statListList[bestIndex][0]);
            Console.WriteLine("latestPositionSize    " + statListList[bestIndex][1]);
            Console.WriteLine("countOfPositions      " + statListList[bestIndex][2]);
            Console.WriteLine("gainPercent           " + statListList[bestIndex][3]);
            Console.WriteLine("alternateGain         " + statListList[bestIndex][4]);
            Console.WriteLine("winningPositionCount  " + statListList[bestIndex][5]);
            Console.WriteLine("losingPositionCount   " + statListList[bestIndex][6]);
            Console.WriteLine("HMA length            " + statListList[bestIndex][8]);
            Console.WriteLine("WMA length            " + statListList[bestIndex][9]);
            Console.WriteLine("LongR length          " + statListList[bestIndex][10]);
            Console.WriteLine("ShortR length         " + statListList[bestIndex][11]);
            Console.WriteLine("Tests done:           " + statListList.Count);

            Console.WriteLine("Best winrate of       " + statListList[statListList.Count - 1][7]);
            Console.WriteLine("At gain of            " + statListList[statListList.Count - 1][3]);
            Console.WriteLine("With parameters       " + statListList[statListList.Count - 1][8] + " " + statListList[statListList.Count - 1][9] + " "
                + statListList[statListList.Count - 1][10] + " " + statListList[statListList.Count - 1][11]);


        }
        private void runLinRegStratOptimizer(List<Frame<DateTime, string>> dataPackage = null)
        {
            Frame<DateTime, string> daatta = null;
            if (dataPackage == null)
            {
                Frame<int, string> data = Frame.ReadCsv("NeutralHold_1HData.csv");
                daatta = data.IndexRows<DateTime>("Key");
            }
            else
            {
                daatta = backtester.PriceDataPackage.First();
            }

            ConcurrentBag<List<IComparable>> statListBag = new();
            int testCounter = 0;
            Console.WriteLine("Parallel task about to begin");

            int from = 4;
            Parallel.For(from, 25,
                from =>
                {

                    Frame<DateTime, string> tempDataFrame = Frame.CreateEmpty<DateTime, string>();
                    Frame<DateTime, string> parallelDaatta = tempDataFrame.Merge(daatta);
                    List<Frame<DateTime, string>> dataPackageList = new() { parallelDaatta };
                    BacktesterOptimizer backOp = new BacktesterOptimizer();
                    backOp.PriceDataPackage = dataPackageList;
                    Strategy.LinRegCurveStrategyOptimizable strat = new Strategy.LinRegCurveStrategyOptimizable();
                    for (int i = 5; i < 28; i++)
                    {
                        for(int j = 5; j < 30; j++)
                        {
                            for(int k = 4; k < 28; k++)
                            {
                                if(j > k)
                                {
                                    int[] options = new int[] { from, i, j, k };
                                    testCounter++;
                                    try
                                    {
                                        //statListBag.Add(backOp.RunBacktest(backOp.PriceDataPackage, strat, options, testCounter));
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Error GET :D");
                                        Console.WriteLine(ex);


                                        Console.WriteLine("What wrong? :D");
                                    }
                                }
                                
                            }
                        }
                    }          
                    
                });

            decimal bestAccValue = 0;
            int bestIndex = 0;
            List<List<IComparable>> statListList = new();
            foreach (List<IComparable> statList in statListBag)
            {
                statListList.Add(statList);
            }
            statListList.Sort((x, y) => x[7].CompareTo(y[7]));
            for (int i = 0; i < statListList.Count; i++)
            {
                if ((decimal)statListList[i][0] > bestAccValue)
                {
                    bestAccValue = (decimal)statListList[i][0];
                    bestIndex = i;
                }
            }

            Console.WriteLine("accountValue          " + statListList[bestIndex][0]);
            Console.WriteLine("latestPositionSize    " + statListList[bestIndex][1]);
            Console.WriteLine("countOfPositions      " + statListList[bestIndex][2]);
            Console.WriteLine("gainPercent           " + statListList[bestIndex][3]);
            Console.WriteLine("alternateGain         " + statListList[bestIndex][4]);
            Console.WriteLine("winningPositionCount  " + statListList[bestIndex][5]);
            Console.WriteLine("losingPositionCount   " + statListList[bestIndex][6]);
            Console.WriteLine("LinReg length         " + statListList[bestIndex][8]);
            Console.WriteLine("WMA length            " + statListList[bestIndex][9]);
            Console.WriteLine("LongR length          " + statListList[bestIndex][10]);
            Console.WriteLine("ShortR length         " + statListList[bestIndex][11]);
            Console.WriteLine("Tests done:           " + statListList.Count);

            Console.WriteLine("Best winrate of       " + statListList[statListList.Count - 1][7]);
            Console.WriteLine("At gain of            " + statListList[statListList.Count - 1][3]);
            Console.WriteLine("With parameters       " + statListList[statListList.Count - 1][8] + " " + statListList[statListList.Count - 1][9] + " "
                + statListList[statListList.Count - 1][10] + " " + statListList[statListList.Count - 1][11]);


        }
        private void runCoppockStratOptimizer(List<Frame<DateTime, string>> dataPackage = null)
        {
            //BacktesterOptimizer backOp = new BacktesterOptimizer();
            //Frame<int, string> data = Frame.ReadCsv("1Y_5MinData.csv");
            //var daatta = data.IndexRows<DateTime>("Key");
            //daatta.Print();
            Frame<DateTime, string> daatta = null;
            if (dataPackage == null)
            {
                Frame<int, string> data = Frame.ReadCsv("NeutralHold_1HData.csv");
                daatta = data.IndexRows<DateTime>("Key");
            }
            else
            {
                daatta = backtester.PriceDataPackage.First();
            }
            
            ConcurrentBag<List<IComparable>> statListBag = new();
            int testCounter = 0;
            Console.WriteLine("Parallel task about to begin");
            
            int from = 5;
            Parallel.For(from, 50,
                from =>
                {

                    Frame<DateTime, string> tempDataFrame = Frame.CreateEmpty<DateTime, string>();
                    Frame<DateTime, string> parallelDaatta = tempDataFrame.Merge(daatta);
                    List<Frame<DateTime, string>> dataPackageList = new() { parallelDaatta};
                    BacktesterOptimizer backOp = new BacktesterOptimizer();
                    backOp.PriceDataPackage = dataPackageList;
                    Strategy.CoppockCurveStrategyOptimizable strat = new Strategy.CoppockCurveStrategyOptimizable();
                    for (int j = 5; j < 30; j++)
                    {
                        for (int k = 4; k < 28; k++)
                        {
                            if (k < j)
                            {  
                            //Console.WriteLine("Starting round {0}, {1}, {2}", from, j, k);
                            testCounter++;
                            try
                            {
                                //statListBag.Add(backOp.RunBacktest(backOp.PriceDataPackage, strat, from, j, k, testCounter));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error GET :D");
                                Console.WriteLine(ex);


                                Console.WriteLine("What wrong? :D");
                            }
                            
                                
                            }
                        }
                    }
                });

            decimal bestAccValue = 0;
            int bestIndex = 0;
            List<List<IComparable>> statListList = new();
            foreach(List<IComparable> statList in statListBag)
            {
                statListList.Add(statList);
            }
            statListList.Sort((x, y) => x[3].CompareTo(y[3]));
            for (int i = 0; i < statListList.Count; i++)
            {
                if ((decimal)statListList[i][0] > bestAccValue)
                {
                    bestAccValue = (decimal)statListList[i][0];
                    bestIndex = i;
                }
            }

            Console.WriteLine("accountValue          " + statListList[bestIndex][0]);
            Console.WriteLine("latestPositionSize    " + statListList[bestIndex][1]);
            Console.WriteLine("countOfPositions      " + statListList[bestIndex][2]);
            Console.WriteLine("gainPercent           " + statListList[bestIndex][3]);
            Console.WriteLine("alternateGain         " + statListList[bestIndex][4]);
            Console.WriteLine("winningPositionCount  " + statListList[bestIndex][5]);
            Console.WriteLine("losingPositionCount   " + statListList[bestIndex][6]);
            Console.WriteLine("WMA                   " + statListList[bestIndex][7]);
            Console.WriteLine("LongR                 " + statListList[bestIndex][8]);
            Console.WriteLine("ShortR                " + statListList[bestIndex][9]);
            Console.WriteLine("Tests done:           " + statListList.Count);

        }

        private void runCipherStratOptimizer(List<Frame<DateTime, string>> dataPackage = null)
        {
            //BacktesterOptimizer backOp = new BacktesterOptimizer();
            //Frame<int, string> data = Frame.ReadCsv("1Y_5MinData.csv");
            //var daatta = data.IndexRows<DateTime>("Key");
            //daatta.Print();
            Frame<DateTime, string> daatta = null;
            Frame<DateTime, string> daatta2 = null;
            if (dataPackage == null)
            {
                Frame<int, string> data = Frame.ReadCsv("NeutralHold_1HData.csv");
                daatta = data.IndexRows<DateTime>("Key");
            }
            else
            {
                daatta = backtester.PriceDataPackage[0];
                daatta2 = backtester.PriceDataPackage[1];

            }

            ConcurrentBag<List<IComparable>> statListBag = new();
            int testCounter = 0;
            Console.WriteLine("Parallel task about to begin");

            int from = 1;
            Parallel.For(from, 200,
                from =>
                {
                    
                    Frame<DateTime, string> tempDataFrame = Frame.CreateEmpty<DateTime, string>();
                    Frame<DateTime, string> parallelDaatta = tempDataFrame.Merge(daatta);

                    Frame<DateTime, string> parallelDaatta2 = tempDataFrame.Merge(daatta2);
                    List<Frame<DateTime, string>> dataPackageList = new() { parallelDaatta, parallelDaatta2 };

                    BacktesterOptimizer backOp = new BacktesterOptimizer();
                    backOp.PriceDataPackage = dataPackageList;
                    decimal ATRMult = 1M + from * 0.05M;
                    Strategy.MarketCipherStrategyOptimizable strat = new Strategy.MarketCipherStrategyOptimizable(ATRMult);
                    Console.WriteLine("Starting round {0}", from);
                    testCounter++;
                    try
                    {
                        statListBag.Add(backOp.RunBacktest(backOp.PriceDataPackage, strat, ATRMult, testCounter));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error GET :D");
                        Console.WriteLine(ex);


                        Console.WriteLine("What wrong? :D");
                    }
                });

            var PlotBuilder = new SeriesBuilder<decimal, decimal>();
            decimal bestAccValue = 0;
            int bestIndex = 0;
            List<List<IComparable>> statListList = new();
            foreach (List<IComparable> statList in statListBag)
            {
                statListList.Add(statList);
            }
            statListList.Sort((x, y) => x[8].CompareTo(y[8]));
            for (int i = 0; i < statListList.Count; i++)
            {
                PlotBuilder.Add((Decimal)statListList[i][8], (Decimal)statListList[i][7]);
                if ((decimal)statListList[i][0] > bestAccValue)
                {
                    bestAccValue = (decimal)statListList[i][0];
                    bestIndex = i;
                }
            }

            var ATRMultPlot = PlotBuilder.Series;
            Frame<decimal, string> kakemans = Frame.CreateEmpty<decimal, string>();
            kakemans.AddColumn("ATR Multiplier",  ATRMultPlot);
            kakemans.SaveCsv("ATRMultData.csv", true);

            Console.WriteLine("accountValue          " + statListList[bestIndex][0]);
            Console.WriteLine("latestPositionSize    " + statListList[bestIndex][1]);
            Console.WriteLine("countOfPositions      " + statListList[bestIndex][2]);
            Console.WriteLine("gainPercent           " + statListList[bestIndex][3]);
            Console.WriteLine("alternateGain         " + statListList[bestIndex][4]);
            Console.WriteLine("winningPositionCount  " + statListList[bestIndex][5]);
            Console.WriteLine("losingPositionCount   " + statListList[bestIndex][6]);
            Console.WriteLine("ATRMult               " + statListList[bestIndex][8]);
            Console.WriteLine("Tests done:           " + statListList.Count);

            Console.WriteLine("Best winrate of       " + statListList[statListList.Count - 1][7]);
            Console.WriteLine("At gain of            " + statListList[statListList.Count - 1][3]);
            Console.WriteLine("With ATRMult       " + statListList[statListList.Count - 1][8]);


        }
        private void runSingleBacktest(List<Frame<DateTime, string>> dataPackage = null)
        {
            Frame<DateTime, string> daatta = null;
            Frame<DateTime, string> daatta2 = null;
            Frame<DateTime, string> daatta3 = null;
            if (dataPackage == null)
            {
                Frame<int, string> data = Frame.ReadCsv("2Y_5MinData.csv");
                Frame<int, string> data2 = Frame.ReadCsv("2Y_30MinData.csv");
                Frame<int, string> data3 = Frame.ReadCsv("2Y_1HData.csv");
                daatta = data.IndexRows<DateTime>("Key");
                daatta2 = data2.IndexRows<DateTime>("Key");
                daatta3 = data3.IndexRows<DateTime>("Key");
                List<Frame<DateTime, string>> dataPackageList = new() { daatta };
                backtester.PriceDataPackage = dataPackageList;
            }

            //Strategy.SuperTrendStochStrategy testStrategy = new Strategy.SuperTrendStochStrategy();
            //Strategy.CoppockCurveStrategy testStrategy = new Strategy.CoppockCurveStrategy((int)WMAControl.Value,
            //   (int)LongRControl.Value, (int)ShortRControl.Value);

            backtester.RunBacktest(backtester.PriceDataPackage, selectedStrategy);
        }

        private void Backtest_Click_1(object sender, EventArgs e)
        {
            runSingleBacktest(backtester.PriceDataPackage);
        }

        private void liveMonitorToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (liveMonitorToggle.Checked == true)
            {
                liveTrader.startLiveMonitoring();
            }
            else
                liveTrader.stopLiveMonitoring();
        }

        private async void WebSocketTest_Click(object sender, EventArgs e)
        {
            WsClient ws = new WsClient();
            await ws.ConnectAsync("wss://data.tradingview.com/socket.io/websocket?from=chart%2F&date=2021_10_25-11_22");
        }
    }
}
