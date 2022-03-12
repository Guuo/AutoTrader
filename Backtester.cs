using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deedle;
using Microsoft.Toolkit.Uwp.Notifications;

namespace AutoTrader
{
    public interface IStrategy
    {
        public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage);
        public string StratName { get; }

        public List<string> Timeframes { get; }
        LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex);
        bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, Strategy.EntrySignal entrySignal);
    }



    public class Backtester
    {

        private List<Frame<DateTime, string>> priceDataPackage = null;

        public List<Frame<DateTime, string>> PriceDataPackage { get => priceDataPackage; set => priceDataPackage = value; }

        public void RunBacktest(List<Frame<DateTime, string>> priceDataPackage, IStrategy tradingStrategy)
        {
            var simTrader = new SimTrader();

            var joinedData = tradingStrategy.PrepData(priceDataPackage);

            Console.WriteLine("         ");
            Console.WriteLine("         ");
            Console.WriteLine("         ");
            Console.WriteLine("         ");


            bool inAPosition = false;
            decimal currentPrice = 0M;
            LiveSignalData signalData = new LiveSignalData(Strategy.EntrySignal.NoSignal);
            joinedData = joinedData.DropSparseRows();

            //skip first 500 candles to avoid inaccuracy in EMA and other indicator calculations
            for (int i = 500; i < joinedData.RowCount; i++)
            {
                var currentRow = joinedData.GetRowAt<decimal>(i);
                var currentDateTime = joinedData.GetRowKeyAt(i);
                
                if (inAPosition == false)
                {
                    signalData = tradingStrategy.CheckEntrySignal(joinedData, i);
                    if (signalData.entrySignal == Strategy.EntrySignal.Long)
                    {
                        currentPrice = currentRow.Get("Close");
                        simTrader.EnterPosition(currentPrice, PositionType.Long, currentDateTime);
                        Console.WriteLine(signalData.entrySignal + " entry at price " + currentPrice + " at DateTime " + currentDateTime);
                        
                        inAPosition = true;

                    }
                    else if (signalData.entrySignal == Strategy.EntrySignal.Short)
                    {
                        currentPrice = currentRow.Get("Close");
                        simTrader.EnterPosition(currentPrice, PositionType.Short, currentDateTime);
                        Console.WriteLine(signalData.entrySignal + " entry at price " + currentRow.Get("Close") + " at DateTime " + currentDateTime);

                        inAPosition = true;
                    }
                } 
                else 
                {
                    //At this point "currentPrice" is a misnomer as it is equivalent the previous buy price, and so it is
                    //passed to the method as the buy price.
                    //The method pulls the ACTUAL current market price from the datarow it is given.
                    if (tradingStrategy.CheckExitSignal(joinedData, i, currentPrice, signalData.entrySignal) == true) 
                    {
                        currentPrice = currentRow.Get("Close");
                        simTrader.ExitPosition(simTrader.positions.Last(), currentPrice, currentDateTime);
                        Console.WriteLine(signalData.entrySignal + " exit at price " + currentRow.Get("Close")
                            +  " (" + simTrader.positions.Last().Returns + "% return) at DateTime "+ currentDateTime);

                        inAPosition = false;
                    }     
                }     
            }
            PrintBackTestStats(simTrader, joinedData);
        }

        public void PrintBackTestStats(SimTrader traderInstance, Frame<DateTime, string> dataSet)
        {
            int winningPositionCount = 0;
            int losingPositionCount = 0;
            int longCount = 0;
            int shortCount = 0;
            int winningLongCount = 0;
            int winningShortCount = 0;
            decimal winRate = 0M;
            var accountValuePlotBuilder = new SeriesBuilder<DateTime, decimal>();

            foreach (SimTrader.Position pos in traderInstance.positions)
            {
                if (pos.PosType == PositionType.Long)
                    longCount++;

                if (pos.Winning == true)
                {
                    winningPositionCount++;
                    if (pos.PosType == PositionType.Long)
                        winningLongCount++;
                    else
                        winningShortCount++;
                }
                else
                    losingPositionCount++;
                accountValuePlotBuilder.Add(pos.EntryTime, pos.PosSize);
                accountValuePlotBuilder.Add(pos.ExitTime, pos.PosSize * ( 1M + pos.Returns / 100M));
            }
            if (traderInstance.positions.Count != 0)
            { 
                winRate = ((decimal)winningPositionCount / (decimal)traderInstance.positions.Count) * 100M;
                shortCount = traderInstance.positions.Count - longCount;
                winningShortCount = winningPositionCount - winningLongCount;
            }
            var accountValuePlot = accountValuePlotBuilder.Series;
            Frame<DateTime, string> kakemans = Frame.CreateEmpty<DateTime, string>();
            kakemans.AddColumn("Value", accountValuePlot);

            

            Console.WriteLine("         ");
            Console.WriteLine("         ");
            Console.WriteLine("         ");
            Console.WriteLine("         ");
            Console.WriteLine("***********");
            Console.WriteLine("***STATS***");
            Console.WriteLine("***********");
            Console.WriteLine("");
            Console.WriteLine("Current account value:    " + traderInstance.Money);
            Console.WriteLine("Latest position size:     " + traderInstance.positions.Last().PosSize);
            Console.WriteLine("Count of positions:       " + traderInstance.positions.Count);
            Console.WriteLine("Winning positions:        " + winningPositionCount);
            Console.WriteLine("Losing positions:         " + losingPositionCount);
            Console.WriteLine("Long positions:           " + longCount);
            Console.WriteLine("Short positions:          " + shortCount);
            Console.WriteLine("Winrate by L/S:           " + ((decimal)winningLongCount / (decimal)longCount) * 100M + "/" + ((decimal)winningShortCount / (decimal)shortCount) * 100M);
            Console.WriteLine("Winrate:                  " + winRate);
            Console.WriteLine("Gain%/Loss%:              " + (100 * ((traderInstance.Money / 500M) - 1M)) + "%");
            Console.WriteLine("Alternate Gain%/Loss%:    " + (100 * ((traderInstance.positions.Last().PosSize / 500M) - 1M)) + "%");
            Console.WriteLine("DateTime range tested:    from " + dataSet.GetRowKeyAt(0) + " to " + dataSet.GetRowKeyAt(dataSet.RowCount - 1));

            //kakemans.SaveCsv("accountvaluedataTSLA.csv", true);
        }

        //Ghetto code here, lazily copied over from the backtesting method, might contain unnecessary code.
        public LiveSignalData RunLiveSignalCheck(List<Frame<DateTime, string>> priceDataPackage, IStrategy tradingStrategy)
        {

            var joinedData = tradingStrategy.PrepData(priceDataPackage);

            bool inAPosition = false;
            decimal currentPrice = 0M;
            
            int lastRowIndex = joinedData.RowCount - 1;
            LiveSignalData signalData = new LiveSignalData(Strategy.EntrySignal.NoSignal);
            var currentRow = joinedData.GetRowAt<decimal>(lastRowIndex);
            //var prevRow = joinedData.GetRowAt<decimal>(lastRowIndex - 1);
            var currentDateTime = joinedData.GetRowKeyAt(lastRowIndex);

            if (inAPosition == false)
            {
                signalData = tradingStrategy.CheckEntrySignal(joinedData, lastRowIndex);
                if (signalData.entrySignal == Strategy.EntrySignal.Long)
                {
                    currentPrice = currentRow.Get("Close");
                    Console.WriteLine(signalData.entrySignal + " entry at price " + currentPrice + " at DateTime " + currentDateTime);
                    inAPosition = true;

                }
                else if (signalData.entrySignal == Strategy.EntrySignal.Short)
                {
                    currentPrice = currentRow.Get("Close"); 
                    Console.WriteLine(signalData.entrySignal + " entry at price " + currentRow.Get("Close") + " at DateTime " + currentDateTime);

                    inAPosition = true;
                }
            }
            
            return signalData;
        }
    }

    public class Strategy
    {
        public enum EntrySignal
        {
            Long,
            Short,
            NoSignal
        }
        public class FirstStrategy : IStrategy
        {
            private string stratName = "FirstStrategy";
            private List<string> timeframes = new();

            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateEMA(dataFrame, 20);
                    CalcTest.CalculateSMA(dataFrame, 50);
                    CalcTest.CalculateStandardDeviation(dataFrame, 10);
                }

                var data5Min = priceDataPackage[0];
                var data1Min = priceDataPackage[1];

                data5Min.DropColumn("High");
                data5Min.DropColumn("Low");
                data1Min.DropColumn("High");
                data1Min.DropColumn("Low");

                data5Min.RenameColumns(str => "5 Min " + str);
                data1Min.RenameColumns(str => "1 Min " + str);
                data1Min.RenameColumn("1 Min Close", "Close");
                //Console.Write(data1Min.Format(500));

                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));


                joinedData = joinedData.FillMissing(Direction.Forward);
                return joinedData;
            }

            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                var currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                var price = currentRow.Get("Close");
                var EMA20_5Min = currentRow.Get("5 Min EMA 20");
                var EMA20_1Min = currentRow.Get("1 Min EMA 20");
                var SMA50_1Min = currentRow.Get("1 Min SMA 50");

                //long entry
                if (price > EMA20_5Min && price > SMA50_1Min && EMA20_1Min > SMA50_1Min)
                {
                    return new LiveSignalData(Strategy.EntrySignal.Long);
                }
                //short entry
                else if (price < EMA20_5Min && price < SMA50_1Min && EMA20_1Min < SMA50_1Min)
                {
                    return new LiveSignalData(Strategy.EntrySignal.Short);
                }
                else
                { 
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal); 
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                var currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                var price = currentRow.Get("Close");
                var SMA50_5Min = currentRow.Get("5 Min SMA 50");

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (price < SMA50_5Min | (price / buyPrice) < 0.996M)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (price > SMA50_5Min | (price / buyPrice) > 1.004M)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class CoppockCurveStrategy : IStrategy
        {
            int WMA;
            int longR;
            int shortR;
            private string stratName = "Coppock curve strategy";
            private List<string> timeframes = new();
            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public CoppockCurveStrategy(int WMA, int longR, int shortR)
            {
                this.WMA = WMA;
                this.longR = longR;
                this.shortR = shortR;

            }

            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateCoppockCurve(dataFrame, WMA, longR, shortR);
                    CalcTest.CalculateLinRegCurve(dataFrame, 14);
                }

                var data5Min = priceDataPackage[0];
                //var data1Min = priceDataPackage[1];

                //data5Min.DropColumn("High");
                //data5Min.DropColumn("Low");

                //data5Min.RenameColumns(str => "5 Min " + str);
                //data5Min.RenameColumn("5 Min Close", "Close");
                //data1Min.RenameColumns(str => "1 Min " + str);
                //Console.Write(data1Min.Format(500));
                /*
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));

                data5Min = data5Min.FillMissing(Direction.Forward);
                
                return joinedData;
                */

                return data5Min;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                var currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                var prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                var price = currentRow.Get("Close");
                var currentCoppock = currentRow.Get("Coppock curve");
                var prevCoppock = prevRow.Get("Coppock curve");
                var time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);

                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose )
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

                //long entry
                if (currentCoppock > prevCoppock)
                {
                    return new LiveSignalData(Strategy.EntrySignal.Long);
                }
                //short entry
                else if (currentCoppock < prevCoppock)
                {
                    return new LiveSignalData(Strategy.EntrySignal.Short);
                }
                else
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                var currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                var prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                var prevPrevRow = dataFrame.GetRowAt<decimal>(rowIndex - 2);
                var currentPrice = currentRow.Get("Close");            
                var currentCoppock = currentRow.Get("Coppock curve");
                var prevCoppock = prevRow.Get("Coppock curve");
                var prevPrevCoppock = prevPrevRow.Get("Coppock curve");



                switch (entrySignal)
                {
                    // to set min profit target & stop loss: && (100 * ((currentPrice - buyPrice) / buyPrice)) > 0.1M |
                    // (100 * ((currentPrice - buyPrice) / buyPrice)) < -0.03M

                    case EntrySignal.Long:
                        {
                            if (currentCoppock < prevCoppock)
                            {
                                return true;
                            }
                            break;
                        }
                        //&& (100 * ((buyPrice / currentPrice) - 1M)) > 0.1M |
                        //(100 * ((buyPrice / currentPrice) - 1M)) < -0.03M
                    case EntrySignal.Short:
                        {
                            if (currentCoppock > prevCoppock)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class LinRegCurveStrategy : IStrategy
        {
            int linRegLength;
            int longR;
            int shortR;
            int WMA;
            private string stratName = "LinRegCurve Strategy";
            private List<string> timeframes = new();

            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public LinRegCurveStrategy(int linRegLength, int WMA, int longR, int shortR)
            {
                this.linRegLength = linRegLength;
                this.longR = longR;
                this.shortR = shortR;
                this.WMA = WMA;


            }
            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateCoppockCurve(dataFrame, WMA, longR, shortR);
                    CalcTest.CalculateLinRegCurve(dataFrame, linRegLength);
                }

                var data = priceDataPackage[0];
                //var data1Min = priceDataPackage[1];
                //Console.Write(data.Format(20));
                //data5Min.RenameColumns(str => "5 Min " + str);
                //data5Min.RenameColumn("5 Min Close", "Close");
                //data1Min.RenameColumns(str => "1 Min " + str);

                /*
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));

                data5Min = data5Min.FillMissing(Direction.Forward);
                
                return joinedData;
                */
                return data;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentLinReg = 0;
                decimal prevLinReg = 0;

                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    currentLinReg = currentRow.Get("LinReg curve");
                    prevLinReg = prevRow.Get("LinReg curve");
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

                //long entry
                if (currentLinReg > prevLinReg && currentCoppock > prevCoppock)
                {
                    return new LiveSignalData(Strategy.EntrySignal.Long);
                }
                //short entry
                else if (currentLinReg < prevLinReg && currentCoppock < prevCoppock)
                {
                    return new LiveSignalData(Strategy.EntrySignal.Short);
                }
                else
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentLinReg = 0;
                decimal prevLinReg = 0;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    currentLinReg = currentRow.Get("LinReg curve");
                    prevLinReg = prevRow.Get("LinReg curve");
                    currentPrice = currentRow.Get("Close");
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentLinReg < prevLinReg && currentCoppock < prevCoppock)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentLinReg > prevLinReg && currentCoppock > prevCoppock)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }
        public class CoppockHullStrategy : IStrategy
        {
            int HMALength;
            int longR;
            int shortR;
            int WMA;
            private string stratName = "CoppockHull Strategy";
            private List<string> timeframes = new();
            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public CoppockHullStrategy(int HMALength, int WMA, int longR, int shortR)
            {
                this.HMALength = HMALength;
                this.longR = longR;
                this.shortR = shortR;
                this.WMA = WMA;
            }

                public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateHMA(dataFrame, HMALength);
                    CalcTest.CalculateCoppockCurve(dataFrame, WMA, longR, shortR);
                }

                var data = priceDataPackage[0];
                //var data1Min = priceDataPackage[1];
                //Console.Write(data.Format(20));
                //data5Min.RenameColumns(str => "5 Min " + str);
                //data5Min.RenameColumn("5 Min Close", "Close");
                //data1Min.RenameColumns(str => "1 Min " + str);

                /*
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));

                data5Min = data5Min.FillMissing(Direction.Forward);
                
                return joinedData;
                */
                return data;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentHMA = 0;
                decimal prevHMA = 0;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    currentHMA = currentRow.Get("HMA");
                    prevHMA = prevRow.Get("HMA");
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

                //long entry
                if (currentHMA > prevHMA && currentCoppock > prevCoppock)
                {
                    return new LiveSignalData(Strategy.EntrySignal.Long);
                }
                //short entry
                else if (currentHMA < prevHMA && currentCoppock < prevCoppock)
                {
                    return new LiveSignalData(Strategy.EntrySignal.Short);
                }
                else
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentHMA = 0;
                decimal prevHMA = 0;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    currentHMA = currentRow.Get("HMA");
                    prevHMA = prevRow.Get("HMA");
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentCoppock < prevCoppock)//&& currentCoppock < prevCoppock
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentCoppock > prevCoppock)//&& currentCoppock > prevCoppock
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class SuperTrendStochStrategy : IStrategy
        {
            private string stratName = "Stochastic 1 w/ 5 + 15min stoch";
            decimal stopLoss;
            decimal takeProfit;
            private List<string> timeframes = new();
            
            public SuperTrendStochStrategy()
            {
                
                Timeframes.Add("1");
                Timeframes.Add("5");
            }
            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateHMA(dataFrame, 50);
                    //CalcTest.CalculateHMA(dataFrame, 200);
                    //CalcTest.CalculateEMA(dataFrame, 200);
                    CalcTest.CalculateSuperTrend(dataFrame, 12, 3);
                    CalcTest.CalculateSuperTrend(dataFrame, 11, 2);
                    //CalcTest.CalculateSuperTrend(dataFrame, 10, 1);
                    CalcTest.CalculateATR(dataFrame, 14);
                    CalcTest.CalculateStochRSI(dataFrame, 14, 3, 3);
                    CalcTest.CalculateStochRSI(dataFrame, 70, 12, 6);
                    CalcTest.CalculateStochRSI(dataFrame, 210, 15, 6);
                }
                
                var data1Min = priceDataPackage[0];
                //data.Print();
                var data5Min = priceDataPackage[1];
                //Console.Write(data.Format(20));

                data1Min.RenameColumns(str =>
                {
                    if (!str.Equals("1 Min " + str))
                    {
                        return "1 Min " + str;
                    }
                    else
                        return str;
                });
                data1Min.RenameColumn("1 Min Close", "Close");
                
                data5Min.RenameColumns(str =>
                {
                    if (!str.Equals("5 Min " + str))
                    {
                        return "5 Min " + str;
                    }
                    else
                        return str;
                });
                

                data5Min = data5Min.Shift(1);
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                joinedData = joinedData.FillMissing(Direction.Forward);
                
                return joinedData;
                
                //return data1Min;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                //decimal EMA200 = 0;
                //decimal superTrend1 = 0;
                decimal superTrend2 = 0;
                decimal superTrend3 = 0;
                decimal stochK1Min = 0;
                decimal stochD1Min = 0;
                decimal close = 0;
                //decimal low = 0;
                //decimal high = 0;
                decimal ATR1Min = 0;

                decimal ATR5Min = 0;
                decimal curHMA50 = 0;
                decimal prevHMA50 = 0;
                decimal stochK5Min = 0;
                decimal stochD5Min = 0;
                decimal stochK15Min = 0;
                decimal stochD15Min = 0;
                // decimal HMA200 = 0;

                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);
                
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    //superTrend1 = currentRow.Get("5 Min SuperTrend 10, 1");
                    superTrend2 = currentRow.Get("1 Min SuperTrend 11, 2");
                    superTrend3 = currentRow.Get("5 Min SuperTrend 12, 3");
                    stochK1Min = currentRow.Get("1 Min Stoch RSI k 14");
                    stochD1Min = currentRow.Get("1 Min Stoch RSI d 14");
                    ATR5Min = currentRow.Get("5 Min ATR 14");
                    ATR1Min = currentRow.Get("1 Min ATR 14");
                    close = currentRow.Get("Close");
                    //low = currentRow.Get("5 Min Low");
                    //high = currentRow.Get("5 Min High");
                    curHMA50 = currentRow.Get("1 Min HMA 50");
                    prevHMA50 = prevRow.Get("1 Min HMA 50");
                    //HMA200 = currentRow.Get("HMA 200");
                    //EMA200 = currentRow.Get("1 Min EMA 200");
                    stochK5Min = currentRow.Get("1 Min Stoch RSI k 70");
                    stochD5Min = currentRow.Get("1 Min Stoch RSI d 70");
                    stochK15Min = currentRow.Get("1 Min Stoch RSI k 210");
                    stochD15Min = currentRow.Get("1 Min Stoch RSI d 210");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

                //long entry
                /*
                int longSuperTrends = 0;
                if (close > superTrend1)
                {
                    
                    longSuperTrends++;
                }
                    
                if (close > superTrend2)
                {
                    
                    longSuperTrends++;

                }

                if (close > superTrend3)
                {
                    
                    longSuperTrends++;
                }
                */
                if (stochK1Min < 20 && stochD1Min < 20 && 
                    curHMA50 / prevHMA50 > 1)//  && close > EMA longSuperTrends >= 1 &&
                {
                    stopLoss = close - ATR1Min * 1M;
                    takeProfit = close + ATR1Min * 1.5M;
                    //decimal riskRange = (close - Math.Min(Math.Min(superTrend2, superTrend3), superTrend1)) * 1.1M; //Math.Min(ATR * 1.5M, close - low);
                    //stopLoss = close - riskRange;
                    //takeProfit = close + riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Long, close, stopLoss, takeProfit);
                    return signalData;


                }
                //short entry
                else if ((stochK1Min > 80 && stochD1Min > 80 &&
                    curHMA50 / prevHMA50 < 1)) //  longSuperTrends <= 2 && close < HMA100
                {
                    stopLoss = close + ATR1Min * 1M;
                    takeProfit = close - ATR1Min * 1.5M;
                    //decimal riskRange = (Math.Max(Math.Max(superTrend2, superTrend3), superTrend1) - close) * 1.1M; //Math.Max(ATR * 1.5M, high - close);
                    //stopLoss = close + riskRange;
                    //takeProfit = close - riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Short, close, stopLoss, takeProfit);
                    return signalData;
          
                }
                else
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class SuperTrendStochStrategy2 : IStrategy
        {
            private string stratName = "Stochastic 1/5/15";
            decimal stopLoss;
            decimal takeProfit;

            private List<string> timeframes = new();

            public SuperTrendStochStrategy2()
            {
                Timeframes.Add("1");
                Timeframes.Add("5");
                Timeframes.Add("15");
            }
            public string StratName { get => stratName; set => stratName = value; }

            public List<string> Timeframes { get => timeframes; set => timeframes = value; }
            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateHMA(dataFrame, 100);
                    //CalcTest.CalculateHMA(dataFrame, 200);
                    //CalcTest.CalculateEMA(dataFrame, 50);
                    //CalcTest.CalculateSuperTrend(dataFrame, 12, 3);
                    //CalcTest.CalculateSuperTrend(dataFrame, 11, 2);
                    //CalcTest.CalculateSuperTrend(dataFrame, 10, 1);
                    CalcTest.CalculateATR(dataFrame, 14);
                    CalcTest.CalculateStochRSI(dataFrame, 14, 3, 3);
                }

                var data1Min = priceDataPackage[0];
                //data.Print();
                var data5Min = priceDataPackage[1];
                var data15Min = priceDataPackage[2];
                //Console.Write(data.Format(20));
                data1Min.RenameColumns(str =>
                {
                    if (!str.Equals("1 Min " + str))
                    {
                        return "1 Min " + str;
                    }
                    else
                        return str;
                });
                data1Min.RenameColumn("1 Min Close", "Close");

                data5Min.RenameColumns(str =>
                {
                    if (!str.Equals("5 Min " + str))
                    {
                        return "5 Min " + str;
                    }
                    else
                        return str;
                });

                data15Min.RenameColumns(str =>
                {
                    if (!str.Equals("15 Min " + str))
                    {
                        return "15 Min " + str;
                    }
                    else
                        return str;
                });

                //shift the higher timerframe data to prevent the backtest from seeing into the future
                //data15Min = data15Min.Shift(1);
                //data5Min = data5Min.Shift(1);
                var joinedData = data15Min.Join(data5Min, JoinKind.Outer);
                joinedData = joinedData.Join(data1Min, JoinKind.Outer);
                joinedData = joinedData.FillMissing(Direction.Forward);
                //Console.Write(joinedData.Format(50));
                return joinedData;

                //return data5Min;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                //Series<string, decimal> prevRow = null;
                //Series<string, decimal> prevRow = null;
                decimal stochK1Min = 0;
                decimal stochD1Min = 0;
                decimal close = 0;
                decimal ATR5Min = 0;
                decimal stochK5Min = 0;
                decimal stochD5Min = 0;
                decimal stochK15Min = 0;
                decimal stochD15Min = 0;
                decimal HMA100 = 0;

                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    //prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    stochK1Min = currentRow.Get("1 Min Stoch RSI k 14");
                    stochD1Min = currentRow.Get("1 Min Stoch RSI d 14");
                    stochK5Min = currentRow.Get("5 Min Stoch RSI k 14");
                    stochD5Min = currentRow.Get("5 Min Stoch RSI d 14");
                    ATR5Min = currentRow.Get("5 Min ATR 14");
                    close = currentRow.Get("Close");
                    HMA100 = currentRow.Get("5 Min HMA 100");
                    //HMA200 = currentRow.Get("HMA 200");
                    //EMA = currentRow.Get("15 Min EMA 50");
                    stochK15Min = currentRow.Get("15 Min Stoch RSI k 14");
                    stochD15Min = currentRow.Get("15 Min Stoch RSI d 14");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

                //long entry
                if ((stochK5Min > stochD5Min && stochK5Min < 60 && stochK15Min > stochD15Min && stochK1Min > stochD1Min && stochK1Min < 80 ))//  && close > EMA longSuperTrends >= 1 &&
                {
                    stopLoss = close - ATR5Min * 1.5M;
                    takeProfit = close + ATR5Min * 1.5M;
                    //decimal riskRange = (close - Math.Min(Math.Min(superTrend2, superTrend3), superTrend1)) * 1.1M; //Math.Min(ATR * 1.5M, close - low);
                    //stopLoss = close - riskRange;
                    //takeProfit = close + riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Long, close, stopLoss, takeProfit);
                    return signalData;


                }
                //short entry
                else if ((stochK5Min < stochD5Min && stochK5Min > 60 && stochK15Min < stochD15Min && stochK1Min < stochD1Min && stochK1Min > 20 )) //  longSuperTrends <= 2 && close < HMA100
                {
                    stopLoss = close + ATR5Min * 1.5M;
                    takeProfit = close - ATR5Min * 1.5M;
                    //decimal riskRange = (Math.Max(Math.Max(superTrend2, superTrend3), superTrend1) - close) * 1.1M; //Math.Max(ATR * 1.5M, high - close);
                    //stopLoss = close + riskRange;
                    //takeProfit = close - riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Short, close, stopLoss, takeProfit);
                    return signalData;

                }
                else
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class SuperTrendStochStrategy3 : IStrategy
        {
            private string stratName = "Stochastic 5/15/60";
            decimal stopLoss;
            decimal takeProfit;

            private List<string> timeframes = new();

            public SuperTrendStochStrategy3()
            {
                Timeframes.Add("5");
                Timeframes.Add("15");
                Timeframes.Add("60");
            }
            public string StratName { get => stratName; set => stratName = value; }

            public List<string> Timeframes { get => timeframes; set => timeframes = value; }
            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    //CalcTest.CalculateHMA(dataFrame, 100);
                    //CalcTest.CalculateHMA(dataFrame, 200);
                    //CalcTest.CalculateEMA(dataFrame, 50);
                    //CalcTest.CalculateSuperTrend(dataFrame, 12, 3);
                    //CalcTest.CalculateSuperTrend(dataFrame, 11, 2);
                    //CalcTest.CalculateSuperTrend(dataFrame, 10, 1);
                    CalcTest.CalculateATR(dataFrame, 14);
                    CalcTest.CalculateStochRSI(dataFrame, 14, 3, 3);
                }

                var data5Min = priceDataPackage[0];
                //data.Print();
                var data15Min = priceDataPackage[1];
                var data60Min = priceDataPackage[2];
                //Console.Write(data.Format(20));
                data5Min.RenameColumns(str =>
                {
                    if (!str.Equals("5 Min " + str))
                    {
                        return "5 Min " + str;
                    }
                    else
                        return str;
                });
                data5Min.RenameColumn("5 Min Close", "Close");

                data15Min.RenameColumns(str =>
                {
                    if (!str.Equals("15 Min " + str))
                    {
                        return "15 Min " + str;
                    }
                    else
                        return str;
                });

                data60Min.RenameColumns(str =>
                {
                    if (!str.Equals("60 Min " + str))
                    {
                        return "60 Min " + str;
                    }
                    else
                        return str;
                });


                data15Min = data15Min.Shift(1);
                data60Min = data60Min.Shift(1);

                var joinedData = data60Min.Join(data15Min, JoinKind.Outer);
                joinedData = joinedData.Join(data5Min, JoinKind.Outer);
                joinedData = joinedData.FillMissing(Direction.Forward);

                return joinedData;

                //return data5Min;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                //Series<string, decimal> prevRow = null;
                decimal stochK5Min = 0;
                decimal stochD5Min = 0;
                decimal close = 0;
                decimal ATR15Min = 0;
                decimal stochK15Min = 0;
                decimal stochD15Min = 0;
                decimal stochK60Min = 0;
                decimal stochD60Min = 0;
                // decimal HMA200 = 0;

                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    //prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    stochK5Min = currentRow.Get("5 Min Stoch RSI k");
                    stochD5Min = currentRow.Get("5 Min Stoch RSI d");
                    stochK15Min = currentRow.Get("15 Min Stoch RSI k");
                    stochD15Min = currentRow.Get("15 Min Stoch RSI d");
                    ATR15Min = currentRow.Get("15 Min ATR 14");
                    close = currentRow.Get("Close");
                    //HMA100 = currentRow.Get("15 Min HMA 100");
                    //HMA200 = currentRow.Get("HMA 200");
                    //EMA = currentRow.Get("15 Min EMA 50");
                    stochK60Min = currentRow.Get("60 Min Stoch RSI k");
                    stochD60Min = currentRow.Get("60 Min Stoch RSI d");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

                //long entry
                if ((stochK5Min > stochD5Min && stochK5Min < 60 && stochK60Min > stochD60Min && stochK15Min > stochD15Min && stochK15Min < 80))//  && close > EMA longSuperTrends >= 1 &&
                {
                    stopLoss = close - ATR15Min * 1.5M;
                    takeProfit = close + ATR15Min * 1.5M;
                    //decimal riskRange = (close - Math.Min(Math.Min(superTrend2, superTrend3), superTrend1)) * 1.1M; //Math.Min(ATR * 1.5M, close - low);
                    //stopLoss = close - riskRange;
                    //takeProfit = close + riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Long, close, stopLoss, takeProfit);
                    return signalData;


                }
                //short entry
                else if ((stochK5Min < stochD5Min && stochK5Min > 60 && stochK60Min < stochD60Min && stochK15Min < stochD15Min && stochK15Min > 20)) //  longSuperTrends <= 2 && close < HMA100
                {
                    stopLoss = close + ATR15Min * 1.5M;
                    takeProfit = close - ATR15Min * 1.5M;
                    //decimal riskRange = (Math.Max(Math.Max(superTrend2, superTrend3), superTrend1) - close) * 1.1M; //Math.Max(ATR * 1.5M, high - close);
                    //stopLoss = close + riskRange;
                    //takeProfit = close - riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Short, close, stopLoss, takeProfit);
                    return signalData;

                }
                else
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class MarketCipherStrategy : IStrategy
        {
            private string stratName = "MarketCipherBStrategy";
            decimal stopLoss;
            decimal takeProfit;
            bool bullTrend = false;
            bool bearTrend = false;
            //decimal ATRMult;
            private List<string> timeframes = new();

            public MarketCipherStrategy()
            {
                Timeframes.Add("5");
                Timeframes.Add("30");
                //this.ATRMult = ATRMult;
            }
            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    //Console.WriteLine(dataFrame.Format(500));
                    CalcTest.CalculateATR(dataFrame, 14);
                    //CalcTest.CalculateBBands(dataFrame, 20, 2);
                    //CalcTest.CalculateEMA(dataFrame, 200);
                    CalcTest.CalculateWaveTrend(dataFrame, 9, 12);
                    CalcTest.CalculateStochRSI(dataFrame, 14, 3, 3);
                }

                var data5Min = priceDataPackage[0];
                //data.Print();
                var data30Min = priceDataPackage[1];
                //Console.Write(data60Min.Format(200));
                
                data5Min.RenameColumns(str =>
                {
                    if (!str.Equals("5 Min " + str))
                    {
                        return "5 Min " + str;
                    }
                    else
                        return str;
                });
                data5Min.RenameColumn("5 Min Close", "Close");
                
                data30Min.RenameColumns(str =>
                {
                    if (!str.Equals("30 Min " + str))
                    {
                        return "30 Min " + str;
                    }
                    else
                        return str;
                });
                

                data30Min = data30Min.Shift(1);
                var joinedData = data30Min.Join(data5Min, JoinKind.Outer);
                joinedData = joinedData.FillMissing(Direction.Forward);
                //Console.WriteLine(joinedData.Format(400));
                return joinedData;
                

                //return data5Min;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal EMA200 = 0;
                decimal waveTrend1_5Min = 0;
                decimal waveTrend2_5Min = 0;
                decimal waveTrend1_30Min = 0;
                decimal waveTrend2_30Min = 0;
                decimal prevWT1_5Min = 0;
                decimal prevWT2_5Min = 0;
                decimal prevWT1_30Min = 0;
                decimal prevWT2_30Min = 0;
                decimal close = 0;
                //decimal lb = 0;
                //decimal mb = 0;
                //decimal ub = 0;
                decimal low = 0;
                decimal high = 0;
                decimal ATR5Min = 0;
                decimal stochK5Min = 0;
                decimal stochD5Min = 0;
                decimal stochK30Min = 0;
                decimal stochD30Min = 0;


                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    ATR5Min = currentRow.Get("5 Min ATR 14");
                    close = currentRow.Get("Close");
                    waveTrend1_5Min = currentRow.Get("5 Min WaveTrend1");
                    waveTrend2_5Min = currentRow.Get("5 Min WaveTrend2");
                    waveTrend1_30Min = currentRow.Get("30 Min WaveTrend1");
                    waveTrend2_30Min = currentRow.Get("30 Min WaveTrend2");
                    prevWT1_5Min = prevRow.Get("5 Min WaveTrend1");
                    prevWT2_5Min = prevRow.Get("5 Min WaveTrend2");
                    prevWT1_30Min = prevRow.Get("30 Min WaveTrend1");
                    prevWT2_30Min = prevRow.Get("30 Min WaveTrend2");
                    //lb = currentRow.Get("1 Min Lower BBand 20, 2");
                    //mb = currentRow.Get("1 Min Middle BBand 20, 2");
                    //ub = currentRow.Get("1 Min Upper BBand 20, 2");
                    //EMA200 = currentRow.Get("120 Min EMA 200");
                    //low = currentRow.Get("1 Min Low");
                    //high = currentRow.Get("1 Min High");
                    stochD5Min = currentRow.Get("5 Min Stoch RSI d 14");
                    stochK5Min = currentRow.Get("5 Min Stoch RSI k 14");
                    stochD30Min = currentRow.Get("30 Min Stoch RSI d 14");
                    stochK30Min = currentRow.Get("30 Min Stoch RSI k 14");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

                //long entry
                /*
                int longSuperTrends = 0;
                if (close > superTrend1)
                {
                    
                    longSuperTrends++;
                }
                    
                if (close > superTrend2)
                {
                    
                    longSuperTrends++;

                }

                if (close > superTrend3)
                {
                    
                    longSuperTrends++;
                }
                */
                if(waveTrend1_30Min > waveTrend2_30Min)
                {
                    bullTrend = true;
                    bearTrend = false;
                }
                if(waveTrend1_30Min < waveTrend2_30Min)
                {
                    bullTrend = false;
                    bearTrend = true;
                }
                /*
                if(waveTrend1_60Min > waveTrend2_60Min && prevWT1_60Min < prevWT2_60Min)
                {
                    bearTrend = false;
                }
                if (waveTrend1_60Min < waveTrend2_60Min && prevWT1_60Min > prevWT2_60Min)
                {
                    bullTrend = false;
                }
                */
                decimal fastWT = waveTrend1_5Min - waveTrend2_5Min;
                decimal prevFastWT = prevWT1_5Min - prevWT2_5Min;

                if (bullTrend == true && waveTrend1_5Min < 0 && stochK5Min > stochD5Min && stochK5Min < 60)// bullTrend == true &&
                {
                    stopLoss = close - ATR5Min * 10M; //3M
                    takeProfit = close + ATR5Min * 10M; // 4.5M
                    //decimal riskRange = (close - Math.Min(Math.Min(superTrend2, superTrend3), superTrend1)) * 1.1M; //Math.Min(ATR * 1.5M, close - low);
                    //stopLoss = close - riskRange;
                    //takeProfit = close + riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Long, close, stopLoss, takeProfit);
                    return signalData;


                }
                //short entry
                else if (bearTrend == true && waveTrend1_5Min > 0 && stochK5Min < stochD5Min && stochK5Min > 60)//bearTrend == true &&
                {
                    stopLoss = close + ATR5Min * 10M;
                    takeProfit = close - ATR5Min * 10M;
                    //decimal riskRange = (Math.Max(Math.Max(superTrend2, superTrend3), superTrend1) - close) * 1.1M; //Math.Max(ATR * 1.5M, high - close);
                    //stopLoss = close + riskRange;
                    //takeProfit = close - riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Short, close, stopLoss, takeProfit);
                    return signalData;

                }
                else
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class ClassicMACDStrategy : IStrategy
        {
            private string stratName = "Classic MACD";
            decimal stopLoss;
            decimal takeProfit;
            bool bullTrend = false;
            bool bearTrend = false;
            private List<string> timeframes = new();

            public ClassicMACDStrategy()
            {
                Timeframes.Add("30");
                //Timeframes.Add("30");
            }
            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    //Console.WriteLine(dataFrame.Format(500));
                    CalcTest.CalculateATR(dataFrame, 14);
                    //CalcTest.CalculateBBands(dataFrame, 20, 2);
                    CalcTest.CalculateEMA(dataFrame, 95);
                    CalcTest.CalculateMACD(dataFrame, 12, 26, 9);
                }

                var data5Min = priceDataPackage[0];
                //data.Print();
                //var data30Min = priceDataPackage[1];
                //Console.Write(data60Min.Format(200));

                data5Min.RenameColumns(str =>
                {
                    if (!str.Equals("5 Min " + str))
                    {
                        return "5 Min " + str;
                    }
                    else
                        return str;
                });
                data5Min.RenameColumn("5 Min Close", "Close");
                /*
                data30Min.RenameColumns(str =>
                {
                    if (!str.Equals("30 Min " + str))
                    {
                        return "30 Min " + str;
                    }
                    else
                        return str;
                });
                */

                //data30Min = data30Min.Shift(1);
                //var joinedData = data30Min.Join(data5Min, JoinKind.Outer);
                //joinedData = joinedData.FillMissing(Direction.Forward);
                //Console.WriteLine(joinedData.Format(400));
                //return joinedData;


                return data5Min;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal EMA95 = 0;
                decimal close = 0;
                //decimal lb = 0;
                //decimal mb = 0;
                //decimal ub = 0;
                decimal low = 0;
                decimal high = 0;
                decimal ATR5Min = 0;
                decimal macd = 0;
                decimal macdSignal = 0;
                decimal macdHistogram = 0;
                decimal prevMacd = 0;
                decimal prevMacdSignal = 0;
                decimal prevMacdHistogram = 0;



                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    ATR5Min = currentRow.Get("5 Min ATR 14");
                    close = currentRow.Get("Close");
                    macd = currentRow.Get("5 Min MACD 12, 26, 9");
                    macdSignal = currentRow.Get("5 Min MACD Signal 12, 26, 9");
                    macdHistogram = currentRow.Get("5 Min MACD Histogram 12, 26, 9");
                    prevMacd = prevRow.Get("5 Min MACD 12, 26, 9");
                    prevMacdSignal = prevRow.Get("5 Min MACD Signal 12, 26, 9");
                    prevMacdHistogram = prevRow.Get("5 Min MACD Histogram 12, 26, 9");
                    EMA95 = currentRow.Get("5 Min EMA 95");
                    //low = currentRow.Get("1 Min Low");
                    //high = currentRow.Get("1 Min High");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

                //long entry

                if (close > EMA95 && macd > macdSignal && prevMacd < prevMacdSignal && macd < 0)// bullTrend == true &&
                {
                    stopLoss = close - ATR5Min * 2.5M;
                    takeProfit = close + ATR5Min * 4M;
                    //decimal riskRange = (close - Math.Min(Math.Min(superTrend2, superTrend3), superTrend1)) * 1.1M; //Math.Min(ATR * 1.5M, close - low);
                    //stopLoss = close - riskRange;
                    //takeProfit = close + riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Long, close, stopLoss, takeProfit);
                    return signalData;


                }
                //short entry
                else if (close < EMA95 && macd < macdSignal && prevMacd > prevMacdSignal && macd > 0)//bearTrend == true &&
                {
                    stopLoss = close + ATR5Min * 2.5M;
                    takeProfit = close - ATR5Min * 4M;
                    //decimal riskRange = (Math.Max(Math.Max(superTrend2, superTrend3), superTrend1) - close) * 1.1M; //Math.Max(ATR * 1.5M, high - close);
                    //stopLoss = close + riskRange;
                    //takeProfit = close - riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Short, close, stopLoss, takeProfit);
                    return signalData;

                }
                else
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }
        public class TradeProStrategy : IStrategy
        {
            decimal stopLoss;
            decimal takeProfit;
            private string stratName = "TradePro triple EMA/Stoch Strategy";
            private List<string> timeframes = new();

            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public TradeProStrategy()
            {
                Timeframes.Add("5");
            }
            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateEMA(dataFrame, 8);
                    CalcTest.CalculateEMA(dataFrame, 14);
                    CalcTest.CalculateEMA(dataFrame, 50);
                    CalcTest.CalculateATR(dataFrame, 14);
                    CalcTest.CalculateStochRSI(dataFrame, 14, 3, 3);
                }

                var data = priceDataPackage[0];
                //data.Print();
                //var data1Min = priceDataPackage[1];
                //Console.Write(data.Format(20));
                //data5Min.RenameColumns(str => "5 Min " + str);
                //data5Min.RenameColumn("5 Min Close", "Close");
                //data1Min.RenameColumns(str => "1 Min " + str);

                /*
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));

                data5Min = data5Min.FillMissing(Direction.Forward);
                
                return joinedData;
                */
                return data;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                //Series<string, decimal> prevRow = null;
                decimal EMA8 = 0;
                decimal EMA14 = 0;
                decimal EMA50 = 0;
                decimal stochK = 0;
                decimal stochD = 0;
                decimal close = 0;
                decimal low = 0;
                decimal high = 0;
                decimal ATR = 0;
                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    //prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;

                    stochK = currentRow.Get("Stoch RSI k 14");
                    stochD = currentRow.Get("Stoch RSI d 14");
                    ATR = currentRow.Get("ATR 14");
                    close = currentRow.Get("Close");
                    low = currentRow.Get("Low");
                    high = currentRow.Get("High");
                    EMA8 = currentRow.Get("EMA 8");
                    EMA14 = currentRow.Get("EMA 14");
                    EMA50 = currentRow.Get("EMA 50");


                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

                //long entry

                if ((EMA8 > EMA14 && EMA14 > EMA50 && stochK > stochD && stochK < 60 && close > EMA8))
                {
                    stopLoss = close - ATR * 1.5M;
                    takeProfit = close + ATR * 1.5M;
                    //Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    return new LiveSignalData(Strategy.EntrySignal.Long);

                }
                //short entry
                else if ((EMA8 < EMA14 && EMA14 < EMA50 && stochK < stochD && stochK > 60 && close < EMA8))
                {
                    stopLoss = close + ATR * 1.5M;
                    takeProfit = close - ATR * 1.5M;
                    //Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    return new LiveSignalData(Strategy.EntrySignal.Short);
                }
                else
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }
        public class RSIDivergenceStrategy : IStrategy
        {
            decimal stopLoss;
            decimal takeProfit;
            private string stratName = "RSI Divergence Strategy";
            private List<string> timeframes = new();

            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public RSIDivergenceStrategy()
            {
                Timeframes.Add("5");
            }
            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateATR(dataFrame, 14);
                    CalcTest.CalculateRSI(dataFrame, 14);
                    CalcTest.CalculateRSIDivergence(dataFrame, "Close", "RSI 14");
                }

                var data = priceDataPackage[0];
                //data.Print();
                //var data1Min = priceDataPackage[1];
                //Console.Write(data.Format(20));
                //data5Min.RenameColumns(str => "5 Min " + str);
                //data5Min.RenameColumn("5 Min Close", "Close");
                //data1Min.RenameColumns(str => "1 Min " + str);

                /*
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));

                data5Min = data5Min.FillMissing(Direction.Forward);
                
                return joinedData;
                */
                return data;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                //Series<string, decimal> prevRow = null;
                decimal close = 0;
                decimal RSI = 0;
                decimal RSIBullDiv = 0;
                decimal RSIBearDiv = 0;
                decimal ATR = 0;
                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    //prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    ATR = currentRow.Get("ATR 14");
                    close = currentRow.Get("Close");
                    RSI = currentRow.Get("RSI 14");
                    RSIBullDiv = currentRow.Get("RSI 14 bulldiv");
                    RSIBearDiv = currentRow.Get("RSI 14 beardiv");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

                //long entry

                if (RSIBullDiv == 1)
                {
                    stopLoss = close - ATR * 1.5M;
                    takeProfit = close + ATR * 1.5M;
                    //Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    return new LiveSignalData(Strategy.EntrySignal.Long);

                }
                //short entry
                else if (RSIBearDiv == 1)
                {
                    stopLoss = close + ATR * 1.5M;
                    takeProfit = close - ATR * 1.5M;
                    //Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    return new LiveSignalData(Strategy.EntrySignal.Short);
                }
                else
                {
                    return new LiveSignalData(Strategy.EntrySignal.NoSignal);
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class ADXBBStrategy : IStrategy
        {
            private string stratName = "ADXBBStrategy";
            decimal stopLoss;
            decimal takeProfit;
            bool AOBackToNeutral;
            //decimal ATRMult;
            private List<string> timeframes = new();

            public ADXBBStrategy()
            {
                Timeframes.Add("5");
                //Timeframes.Add("30");
                //this.ATRMult = ATRMult;
            }
            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    //Console.WriteLine(dataFrame.Format(500));
                    CalcTest.CalculateATR(dataFrame, 14);
                    CalcTest.CalculateBBands(dataFrame, 20, 1);
                    CalcTest.CalculateEMA(dataFrame, 5);
                    CalcTest.CalculateEMA(dataFrame, 21);
                    CalcTest.CalculateEMA(dataFrame, 50);
                    CalcTest.CalculateEMA(dataFrame, 200);

                    CalcTest.CalculateWaveTrend(dataFrame, 9, 12);
                    CalcTest.CalculateADX(dataFrame, 14);
                    CalcTest.CalculateAO(dataFrame);
                }

                var data5Min = priceDataPackage[0];
                //data.Print();
                //var data30Min = priceDataPackage[1];
                //Console.Write(data60Min.Format(200));

                data5Min.RenameColumns(str =>
                {
                    if (!str.Equals("5 Min " + str))
                    {
                        return "5 Min " + str;
                    }
                    else
                        return str;
                });
                data5Min.RenameColumn("5 Min Close", "Close");
                /*
                data30Min.RenameColumns(str =>
                {
                    if (!str.Equals("30 Min " + str))
                    {
                        return "30 Min " + str;
                    }
                    else
                        return str;
                });

                */
                //data30Min = data30Min.Shift(1);
                //var joinedData = data30Min.Join(data5Min, JoinKind.Outer);
                //joinedData = joinedData.FillMissing(Direction.Forward);
                //Console.WriteLine(joinedData.Format(400));
                //return joinedData;


                return data5Min;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal EMA5 = 0;
                decimal EMA21 = 0;
                decimal EMA50 = 0;
                decimal EMA200 = 0;

                decimal waveTrend1_5Min = 0;
                decimal waveTrend2_5Min = 0;
                decimal waveTrend1_30Min = 0;
                decimal waveTrend2_30Min = 0;
                decimal prevWT1_5Min = 0;
                decimal prevWT2_5Min = 0;
                decimal prevWT1_30Min = 0;
                decimal prevWT2_30Min = 0;
                decimal close = 0;
                decimal ADX = 0;
                decimal AO = 0;
                
                decimal lb = 0;
                decimal mb = 0;
                decimal ub = 0;
                decimal low = 0;
                decimal high = 0;
                decimal ATR5Min = 0;
                //decimal stochK5Min = 0;
                //decimal stochD5Min = 0;
                //decimal stochK30Min = 0;
                //decimal stochD30Min = 0;


                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    ATR5Min = currentRow.Get("5 Min ATR 14");
                    close = currentRow.Get("Close");
                    waveTrend1_5Min = currentRow.Get("5 Min WaveTrend1");
                    waveTrend2_5Min = currentRow.Get("5 Min WaveTrend2");
                    //waveTrend1_30Min = currentRow.Get("30 Min WaveTrend1");
                    //waveTrend2_30Min = currentRow.Get("30 Min WaveTrend2");
                    prevWT1_5Min = prevRow.Get("5 Min WaveTrend1");
                    prevWT2_5Min = prevRow.Get("5 Min WaveTrend2");
                   //prevWT1_30Min = prevRow.Get("30 Min WaveTrend1");
                   //prevWT2_30Min = prevRow.Get("30 Min WaveTrend2");
                    ADX = currentRow.Get("5 Min ADX 14");
                    AO = currentRow.Get("5 Min AO");

                    lb = currentRow.Get("5 Min Lower BBand 20, 1");
                    mb = currentRow.Get("5 Min Middle BBand 20, 1");
                    ub = currentRow.Get("5 Min Upper BBand 20, 1");
                    EMA200 = currentRow.Get("5 Min EMA 200");
                    EMA50 = currentRow.Get("5 Min EMA 50");
                    EMA21 = currentRow.Get("5 Min EMA 21");
                    EMA5 = currentRow.Get("5 Min EMA 5");
                    //low = currentRow.Get("1 Min Low");
                    //high = currentRow.Get("1 Min High");
                    //stochD5Min = currentRow.Get("5 Min Stoch RSI d 14");
                    //stochK5Min = currentRow.Get("5 Min Stoch RSI k 14");
                    //stochD30Min = currentRow.Get("30 Min Stoch RSI d 14");
                    //stochK30Min = currentRow.Get("30 Min Stoch RSI k 14");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

                if (AO < close * 0.0008M && AO > close * -0.0008M)
                    AOBackToNeutral = true;
               

                if (close > ub && ADX > 15 && AO > close * 0.0008M)// oil toimii EMAn kanssa, indeksit ilman
                {
                    stopLoss = close - ATR5Min * 3M; //3M
                    takeProfit = close + ATR5Min * 3M; // 4.5M
                    //decimal riskRange = (close - Math.Min(Math.Min(superTrend2, superTrend3), superTrend1)) * 1.1M; //Math.Min(ATR * 1.5M, close - low);
                    //stopLoss = close - riskRange;
                    //takeProfit = close + riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Long, close, stopLoss, takeProfit);
                    
                    return signalData;


                }
                //short entry
                else if (close < lb && ADX > 15 && AO < -(close * 0.0008M)) //&& EMA5 < EMA21 && EMA50 < EMA200 
                {
                    stopLoss = close + ATR5Min * 3M;
                    takeProfit = close - ATR5Min * 3M;
                    //decimal riskRange = (Math.Max(Math.Max(superTrend2, superTrend3), superTrend1) - close) * 1.1M; //Math.Max(ATR * 1.5M, high - close);
                    //stopLoss = close + riskRange;
                    //takeProfit = close - riskRange * 1M;
                    Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Short, close, stopLoss, takeProfit);
                    
                    return signalData;

                }
                else
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }
        public class CoppockCurveStrategyOptimizable
        {
            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage, int WMA, int LongR,
                int ShortR)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateCoppockCurve(dataFrame, WMA, LongR, ShortR);
                    //CalcTest.CalculateLinRegCurve(dataFrame, 14);
                }

                var data = priceDataPackage[0];
                //var data1Min = priceDataPackage[1];
                //Console.Write(data.Format(20));
                //data5Min.RenameColumns(str => "5 Min " + str);
                //data5Min.RenameColumn("5 Min Close", "Close");
                //data1Min.RenameColumns(str => "1 Min " + str);
                
                /*
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));

                data5Min = data5Min.FillMissing(Direction.Forward);
                
                return joinedData;
                */
                return data;
            }
            public EntrySignal CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");

                }
            catch (Exception ex)
            {
                Console.WriteLine("Error");
                dataFrame.Print();
                Console.WriteLine(ex);

            }
                    //No trading at night when NordNet is not open
                    if (time < marketOpen | time > marketClose)
                    {
                        return EntrySignal.NoSignal;
                    }

                    //long entry
                    if (currentCoppock > prevCoppock)
                    {
                        return EntrySignal.Long;
                    }
                    //short entry
                    else if (currentCoppock < prevCoppock)
                    {
                        return EntrySignal.Short;
                    }
                    else
                    {
                        return EntrySignal.NoSignal;
                    }
                
            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                decimal currentPrice = 0;
                
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentCoppock < prevCoppock)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentCoppock > prevCoppock)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class LinRegCurveStrategyOptimizable
        {
            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage, int linRegLength, int WMA, int longR,
                int shortR)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateLinRegCurve(dataFrame, linRegLength);
                    CalcTest.CalculateCoppockCurve(dataFrame, WMA, longR, shortR);
                }

                var data = priceDataPackage[0];
                //var data1Min = priceDataPackage[1];
                //Console.Write(data.Format(20));
                //data5Min.RenameColumns(str => "5 Min " + str);
                //data5Min.RenameColumn("5 Min Close", "Close");
                //data1Min.RenameColumns(str => "1 Min " + str);

                /*
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));

                data5Min = data5Min.FillMissing(Direction.Forward);
                
                return joinedData;
                */
                return data;
            }
            public EntrySignal CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentLinReg = 0;
                decimal prevLinReg = 0;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    currentLinReg = currentRow.Get("LinReg curve");
                    prevLinReg = prevRow.Get("LinReg curve");
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    return EntrySignal.NoSignal;
                }

                //long entry
                if (currentLinReg > prevLinReg && currentCoppock > prevCoppock)
                {
                    return EntrySignal.Long;
                }
                //short entry
                else if (currentLinReg < prevLinReg && currentCoppock < prevCoppock)
                {
                    return EntrySignal.Short;
                }
                else
                {
                    return EntrySignal.NoSignal;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentLinReg = 0;
                decimal prevLinReg = 0;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    currentLinReg = currentRow.Get("LinReg curve");
                    prevLinReg = prevRow.Get("LinReg curve");
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentLinReg < prevLinReg)//&& currentCoppock < prevCoppock
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentLinReg > prevLinReg)//&& currentCoppock > prevCoppock
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class CoppockHullStrategyOptimizable
        {
            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage, int HMALength, int WMA, int longR,
                int shortR)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    CalcTest.CalculateHMA(dataFrame, HMALength);
                    CalcTest.CalculateCoppockCurve(dataFrame, WMA, longR, shortR);
                }

                var data = priceDataPackage[0];
                //var data1Min = priceDataPackage[1];
                //Console.Write(data.Format(20));
                //data5Min.RenameColumns(str => "5 Min " + str);
                //data5Min.RenameColumn("5 Min Close", "Close");
                //data1Min.RenameColumns(str => "1 Min " + str);

                /*
                var joinedData = data5Min.Join(data1Min, JoinKind.Outer);
                //Console.Write(joinedData.Format(50));

                data5Min = data5Min.FillMissing(Direction.Forward);
                
                return joinedData;
                */
                return data;
            }
            public EntrySignal CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentHMA = 0;
                decimal prevHMA = 0;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);
                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    currentHMA = currentRow.Get("HMA");
                    prevHMA = prevRow.Get("HMA");
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    return EntrySignal.NoSignal;
                }

                //long entry
                if (currentHMA > prevHMA && currentCoppock > prevCoppock)
                {
                    return EntrySignal.Long;
                }
                //short entry
                else if (currentHMA < prevHMA && currentCoppock < prevCoppock)
                {
                    return EntrySignal.Short;
                }
                else
                {
                    return EntrySignal.NoSignal;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal currentHMA = 0;
                decimal prevHMA = 0;
                decimal currentCoppock = 0;
                decimal prevCoppock = 0;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    currentHMA = currentRow.Get("HMA");
                    prevHMA = prevRow.Get("HMA");
                    currentCoppock = currentRow.Get("Coppock curve");
                    prevCoppock = prevRow.Get("Coppock curve");
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentCoppock < prevCoppock)//&& currentCoppock < prevCoppock
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentCoppock > prevCoppock)//&& currentCoppock > prevCoppock
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }

        public class MarketCipherStrategyOptimizable : IStrategy
        {
            private string stratName = "MarketCipherBStrategy";
            decimal stopLoss;
            decimal takeProfit;
            bool bullTrend = false;
            bool bearTrend = false;
            private List<string> timeframes = new();
            decimal ATRMult;

            public MarketCipherStrategyOptimizable(decimal atr)
            {
                Timeframes.Add("5");
                Timeframes.Add("30");
                ATRMult = atr;
            }
            public string StratName { get => stratName; set => stratName = value; }
            public List<string> Timeframes { get => timeframes; set => timeframes = value; }

            public Frame<DateTime, string> PrepData(List<Frame<DateTime, string>> priceDataPackage)
            {
                //Frame<DateTime, string> data5Min = Frame.CreateEmpty<DateTime, string>();
                foreach (Frame<DateTime, string> dataFrame in priceDataPackage)
                {
                    //Console.WriteLine(dataFrame.Format(500));
                    CalcTest.CalculateATR(dataFrame, 14);
                    //CalcTest.CalculateBBands(dataFrame, 20, 2);
                    //CalcTest.CalculateEMA(dataFrame, 200);
                    CalcTest.CalculateWaveTrend(dataFrame, 9, 12);
                    CalcTest.CalculateStochRSI(dataFrame, 14, 3, 3);
                }

                var data5Min = priceDataPackage[0];
                //data.Print();
                var data30Min = priceDataPackage[1];
                //Console.Write(data60Min.Format(200));

                data5Min.RenameColumns(str =>
                {
                    if (!str.Equals("5 Min " + str))
                    {
                        return "5 Min " + str;
                    }
                    else
                        return str;
                });
                data5Min.RenameColumn("5 Min Close", "Close");

                data30Min.RenameColumns(str =>
                {
                    if (!str.Equals("30 Min " + str))
                    {
                        return "30 Min " + str;
                    }
                    else
                        return str;
                });


                data30Min = data30Min.Shift(1);
                var joinedData = data30Min.Join(data5Min, JoinKind.Outer);
                joinedData = joinedData.FillMissing(Direction.Forward);
                //Console.WriteLine(joinedData.Format(400));
                return joinedData;


                //return data5Min;
            }
            public LiveSignalData CheckEntrySignal(Frame<DateTime, string> dataFrame, int rowIndex)
            {
                Series<string, decimal> currentRow = null;
                Series<string, decimal> prevRow = null;
                decimal EMA200 = 0;
                decimal waveTrend1_5Min = 0;
                decimal waveTrend2_5Min = 0;
                decimal waveTrend1_30Min = 0;
                decimal waveTrend2_30Min = 0;
                decimal prevWT1_5Min = 0;
                decimal prevWT2_5Min = 0;
                decimal prevWT1_30Min = 0;
                decimal prevWT2_30Min = 0;
                decimal close = 0;
                //decimal lb = 0;
                //decimal mb = 0;
                //decimal ub = 0;
                decimal low = 0;
                decimal high = 0;
                decimal ATR5Min = 0;
                decimal stochK5Min = 0;
                decimal stochD5Min = 0;
                decimal stochK30Min = 0;
                decimal stochD30Min = 0;


                TimeSpan time = new();
                TimeSpan marketOpen = new TimeSpan(9, 15, 0);
                TimeSpan marketClose = new TimeSpan(21, 15, 0);

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    prevRow = dataFrame.GetRowAt<decimal>(rowIndex - 1);
                    time = dataFrame.GetRowKeyAt(rowIndex).TimeOfDay;
                    ATR5Min = currentRow.Get("5 Min ATR 14");
                    close = currentRow.Get("Close");
                    waveTrend1_5Min = currentRow.Get("5 Min WaveTrend1");
                    waveTrend2_5Min = currentRow.Get("5 Min WaveTrend2");
                    waveTrend1_30Min = currentRow.Get("30 Min WaveTrend1");
                    waveTrend2_30Min = currentRow.Get("30 Min WaveTrend2");
                    prevWT1_5Min = prevRow.Get("5 Min WaveTrend1");
                    prevWT2_5Min = prevRow.Get("5 Min WaveTrend2");
                    prevWT1_30Min = prevRow.Get("30 Min WaveTrend1");
                    prevWT2_30Min = prevRow.Get("30 Min WaveTrend2");
                    //lb = currentRow.Get("1 Min Lower BBand 20, 2");
                    //mb = currentRow.Get("1 Min Middle BBand 20, 2");
                    //ub = currentRow.Get("1 Min Upper BBand 20, 2");
                    //EMA200 = currentRow.Get("120 Min EMA 200");
                    //low = currentRow.Get("1 Min Low");
                    //high = currentRow.Get("1 Min High");
                    stochD5Min = currentRow.Get("5 Min Stoch RSI d 14");
                    stochK5Min = currentRow.Get("5 Min Stoch RSI k 14");
                    stochD30Min = currentRow.Get("30 Min Stoch RSI d 14");
                    stochK30Min = currentRow.Get("30 Min Stoch RSI k 14");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }
                //No trading at night when NordNet is not open
                if (time < marketOpen | time > marketClose)
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

                //long entry
                /*
                int longSuperTrends = 0;
                if (close > superTrend1)
                {
                    
                    longSuperTrends++;
                }
                    
                if (close > superTrend2)
                {
                    
                    longSuperTrends++;

                }

                if (close > superTrend3)
                {
                    
                    longSuperTrends++;
                }
                */
                if (waveTrend1_30Min > waveTrend2_30Min)
                {
                    bullTrend = true;
                    bearTrend = false;
                }
                if (waveTrend1_30Min < waveTrend2_30Min)
                {
                    bullTrend = false;
                    bearTrend = true;
                }
                /*
                if(waveTrend1_60Min > waveTrend2_60Min && prevWT1_60Min < prevWT2_60Min)
                {
                    bearTrend = false;
                }
                if (waveTrend1_60Min < waveTrend2_60Min && prevWT1_60Min > prevWT2_60Min)
                {
                    bullTrend = false;
                }
                */
                decimal fastWT = waveTrend1_5Min - waveTrend2_5Min;
                decimal prevFastWT = prevWT1_5Min - prevWT2_5Min;

                if (bullTrend == true && waveTrend1_5Min < 0 && stochK5Min > stochD5Min && stochK5Min < 60)// bullTrend == true &&
                {
                    stopLoss = close - ATR5Min * ATRMult;
                    takeProfit = close + ATR5Min * ATRMult * 1.5M;
                    //decimal riskRange = (close - Math.Min(Math.Min(superTrend2, superTrend3), superTrend1)) * 1.1M; //Math.Min(ATR * 1.5M, close - low);
                    //stopLoss = close - riskRange;
                    //takeProfit = close + riskRange * 1M;
                    //Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Long, close, stopLoss, takeProfit);
                    return signalData;


                }
                //short entry
                else if (bearTrend == true && waveTrend1_5Min > 0 && stochK5Min < stochD5Min && stochK5Min > 60)//bearTrend == true &&
                {
                    stopLoss = close + ATR5Min * ATRMult;
                    takeProfit = close - ATR5Min * ATRMult * 1.5M;
                    //decimal riskRange = (Math.Max(Math.Max(superTrend2, superTrend3), superTrend1) - close) * 1.1M; //Math.Max(ATR * 1.5M, high - close);
                    //stopLoss = close + riskRange;
                    //takeProfit = close - riskRange * 1M;
                    //Console.WriteLine("Set stop loss at " + stopLoss + ", take profit at " + takeProfit);
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.Short, close, stopLoss, takeProfit);
                    return signalData;

                }
                else
                {
                    LiveSignalData signalData = new LiveSignalData(EntrySignal.NoSignal);
                    return signalData;
                }

            }
            public bool CheckExitSignal(Frame<DateTime, string> dataFrame, int rowIndex, decimal buyPrice, EntrySignal entrySignal)
            {
                Series<string, decimal> currentRow = null;
                decimal currentPrice = 0;

                try
                {
                    currentRow = dataFrame.GetRowAt<decimal>(rowIndex);
                    currentPrice = currentRow.Get("Close");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error");
                    dataFrame.Print();
                    Console.WriteLine(ex);

                }

                switch (entrySignal)
                {
                    case EntrySignal.Long:
                        {
                            if (currentPrice < stopLoss || currentPrice > takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                    case EntrySignal.Short:
                        {
                            if (currentPrice > stopLoss || currentPrice < takeProfit)
                            {
                                return true;
                            }
                            break;
                        }
                }

                return false;
            }
        }
    }



    public enum PositionType
    {
        Long,
        Short
    }

    public class SimTrader
    {

        //Maybe add more fields that can be used with the class methods? Such as always having a tick delay before an order is executed
        //to simulate a human having to input an order via webpage
        private decimal money;
        private decimal commissionFeeFlat;
        private decimal commissionFeePercentage;
        private decimal instrumentSpread;
        private decimal commonLeverage;
        private decimal otherTradeFees;
        private decimal preferredPositionSize;

        public List<Position> positions = new List<Position>();


        //Lista positioista tänne, positio lisätää listaan ku se luodaan ja sen indeksi muistetaan tai jotai ja
        //sit positioihin lisätään tieto että "openedDatetime" ja "closedDatetime" ja silleen voidaa
        //pitää lokia kaikista positioista, nuo tiedot pitää kans lisätä kai argumenteiks Position classii tai jotai

        public decimal Money { get => money; set => money = value; }
        public decimal CommissionFeeFlat { get => commissionFeeFlat; set => commissionFeeFlat = value; }
        public decimal CommissionFeePercentage { get => commissionFeePercentage; set => commissionFeePercentage = value; }
        public decimal InstrumentSpread { get => instrumentSpread; set => instrumentSpread = value; }
        public decimal OtherTradeFees { get => otherTradeFees; set => otherTradeFees = value; }
        public decimal CommonLeverage { get => commonLeverage; set => commonLeverage = value; }
        public decimal PreferredPositionSize { get => preferredPositionSize; set => preferredPositionSize = value; }

        public SimTrader(decimal money = 500M, decimal commissionFeeFlat = 0M,
            decimal commissionFeePercentage = 0M, decimal instrumentSpread = 0.0000M,
            decimal otherTradeFees = 0M, decimal commonLeverage = 1M, decimal preferredPositionSize = 1M)
        {
            Money = money;
            CommissionFeeFlat = commissionFeeFlat;
            CommissionFeePercentage = commissionFeePercentage;
            InstrumentSpread = instrumentSpread;
            OtherTradeFees = otherTradeFees;
            CommonLeverage = commonLeverage;
            PreferredPositionSize = preferredPositionSize;
        }

        //EnterPosition() handles the "real" monetary movements that happen, without any leverage applied. Commission fees apply in the 
        //entering method because they affect possible position size.
        //ExitPosition() on the other hand calculates the spread of the instrument as a loss in sale price, as well as any leverage etc.
        public void EnterPosition(decimal currentMarketPrice, PositionType posType, DateTime entryTime)
        {
            decimal positionSize = (Money - CommissionFeeFlat) * PreferredPositionSize;
            Money = Money - (positionSize + CommissionFeeFlat);
            var position = new Position(positionSize, currentMarketPrice, CommonLeverage, posType, entryTime);
            positions.Add(position);
        }
        public void ExitPosition(Position position, decimal currentMarketPrice, DateTime exitTime)
        {
            if(position.PosType == PositionType.Long)
            {
                decimal posSizeAtExit = position.PosSize * (1M + ((currentMarketPrice / position.EntryPrice) - 1M) * position.PosLeverage) * (1M - InstrumentSpread);
                Money = Money + posSizeAtExit;
                position.Returns = 100M * ((posSizeAtExit / position.PosSize) - 1M);
            }
            else if(position.PosType == PositionType.Short)
            {
                decimal posSizeAtExit = position.PosSize * (1M + ((position.EntryPrice / currentMarketPrice) - 1M) * position.PosLeverage) * (1M - InstrumentSpread);
                Money = Money + posSizeAtExit;
                position.Returns = 100M * ((posSizeAtExit / position.PosSize) - 1M);
            }
           
            position.PosState = "Closed";
            position.ExitPrice = currentMarketPrice;
            position.ExitTime = exitTime;
            if (position.Returns < 0)
            {
                position.Winning = false;
            }
            else
                position.Winning = true;

        }

        public class Position
        {
            private decimal posSize;
            private decimal posLeverage;
            private PositionType posType;
            private decimal entryPrice;
            private decimal exitPrice;
            private string posState;
                
            private DateTime entryTime;
            private DateTime exitTime;

            private bool winning;
            //Returns are saved as a percentage, meaning a value of 6,5 represents a return of 6,5%
            private decimal returns;



            public Position(decimal positionSize, decimal buyPrice, decimal leverage, PositionType type, DateTime entry)
            {
                PosSize = positionSize;
                EntryPrice = buyPrice;
                PosLeverage = leverage;
                PosType = type;
                EntryTime = entry;
                PosState = "Open";
            }

            public decimal PosSize { get => posSize; set => posSize = value; }
            public decimal PosLeverage { get => posLeverage; set => posLeverage = value; }
            public PositionType PosType { get => posType; set => posType = value; }
            public decimal EntryPrice { get => entryPrice; set => entryPrice = value; }
            public decimal ExitPrice { get => exitPrice; set => exitPrice = value; }
            public string PosState { get => posState; set => posState = value; }
            public DateTime EntryTime { get => entryTime; set => entryTime = value; }
            public DateTime ExitTime { get => exitTime; set => exitTime = value; }
            public decimal Returns { get => returns; set => returns = value; }
            public bool Winning { get => winning; set => winning = value; }
        }
    }
















    public class BacktesterOptimizer
    {
        private List<Frame<DateTime, string>> priceDataPackage;

        public List<Frame<DateTime, string>> PriceDataPackage { get => priceDataPackage; set => priceDataPackage = value; }

        public List<IComparable> RunBacktest(List<Frame<DateTime, string>> priceDataPackage, Strategy.MarketCipherStrategyOptimizable tradingStrategy,
            decimal options, int testNumber)
        {
            var simTrader = new SimTrader();

            var joinedData = tradingStrategy.PrepData(priceDataPackage);

            //lock (testLock)
            //{


            bool inAPosition = false;
            decimal currentPrice = 0M;
            LiveSignalData signalData = new LiveSignalData(Strategy.EntrySignal.NoSignal);
            joinedData = joinedData.DropSparseRows();

            //skip first 500 candles to avoid inaccuracy in EMA and other indicator calculations

            for (int i = 500; i < joinedData.RowCount; i++)
            {
                var currentRow = joinedData.GetRowAt<decimal>(i);
                var currentDateTime = joinedData.GetRowKeyAt(i);

                if (inAPosition == false)
                {
                    signalData = tradingStrategy.CheckEntrySignal(joinedData, i);
                    if (signalData.entrySignal == Strategy.EntrySignal.Long)
                    {
                        currentPrice = currentRow.Get("Close");
                        simTrader.EnterPosition(currentPrice, PositionType.Long, currentDateTime);
                        //Console.WriteLine(signalData.entrySignal + " entry at price " + currentPrice + " at DateTime " + currentDateTime);

                        inAPosition = true;

                    }
                    else if (signalData.entrySignal == Strategy.EntrySignal.Short)
                    {
                        currentPrice = currentRow.Get("Close");
                        simTrader.EnterPosition(currentPrice, PositionType.Short, currentDateTime);
                        //Console.WriteLine(signalData.entrySignal + " entry at price " + currentRow.Get("Close") + " at DateTime " + currentDateTime);

                        inAPosition = true;
                    }
                }
                else
                {
                    //At this point "currentPrice" is a misnomer as it is equivalent the previous buy price, and so it is
                    //passed to the method as the buy price.
                    //The method pulls the ACTUAL current market price from the datarow it is given.
                    if (tradingStrategy.CheckExitSignal(joinedData, i, currentPrice, signalData.entrySignal) == true)
                    {
                        currentPrice = currentRow.Get("Close");
                        simTrader.ExitPosition(simTrader.positions.Last(), currentPrice, currentDateTime);
                        //Console.WriteLine(signalData.entrySignal + " exit at price " + currentRow.Get("Close")
                        //    + " (" + simTrader.positions.Last().Returns + "% return) at DateTime " + currentDateTime);

                        inAPosition = false;
                    }
                }
            }
            var statList = GetBackTestStats(simTrader, joinedData, options, testNumber);
            
            return statList;
        }

        public List<IComparable> GetBackTestStats(SimTrader traderInstance, Frame<DateTime, string> dataSet, decimal options, int testNumber)
        {
            int winningPositionCount = 0;
            int losingPositionCount = 0;
           
            foreach (SimTrader.Position pos in traderInstance.positions)
            {
                if (pos.Winning == true)
                    winningPositionCount++;
                else
                    losingPositionCount++;
            }
            decimal accountValue = traderInstance.Money;
            decimal latestPositionSize = 0M;
            int countOfPositions = 0;
            decimal alternateGain = 0M;
            decimal winRate = 0M;
            if (traderInstance.positions.Count != 0)
            {
                latestPositionSize = traderInstance.positions.Last().PosSize;
                countOfPositions = traderInstance.positions.Count;
                alternateGain = (100 * ((traderInstance.positions.Last().PosSize / 500M) - 1M));
                winRate = ((decimal)winningPositionCount / (decimal)traderInstance.positions.Count) * 100M;

            }
            if (accountValue == 0M)
            {
                accountValue = latestPositionSize;
            }

            decimal gainPercent = (100 * ((accountValue / 500M) - 1M));

            
            //decimal inactivateCoppockOffset = thresholdOffset;

            List<IComparable> statList = new()
            {
                accountValue,
                latestPositionSize,
                countOfPositions,
                gainPercent,
                alternateGain,
                winningPositionCount,
                losingPositionCount,
                winRate,
                
                options

            };

            Console.WriteLine("Test #" + testNumber + " done with");
            Console.WriteLine("gain {0} and params {1} ", alternateGain, options);
            Console.WriteLine("");

            return statList;
        }

    }
}