using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Media;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Threading;

namespace AutoTrader
{
    class LiveTrader
    {
        DataLoader dataLoader;
        Backtester backtester = new Backtester();
        LiveSignalData signalGenerated;
        System.Timers.Timer timer;
        public Dictionary<string, string> symbolDict;
        public LiveTrader(DataLoader dl)
        {
            dataLoader = dl;
            symbolDict = new Dictionary<string, string>()
            {
                {"NDQ", "8874" },
                {"DAX", "8826" },
                //{"R2K", "8864" },
                //{"SPX", "8839" },
                //{"OIL", "8833" }
            };
        }

        SoundPlayer simpleSound = new SoundPlayer(@"c:\Windows\Media\Windows Notify Calendar.wav");
        public void startLiveMonitoring()
        {
            timer = new System.Timers.Timer(30000);
            timer.Elapsed += CheckForSignals;
            timer.AutoReset = true;
            timer.Enabled = true;
            Console.WriteLine("Live monitoring started.");

        }
        public async void CheckForSignals(Object source, ElapsedEventArgs e)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            DateTime dateTimeFrom = DateTime.UtcNow.AddHours(-83); //83h is under 5k

            foreach(var kvp in symbolDict)
            {
                bool loopFailed = true;
                while (loopFailed)
                {
                    try
                    {
                        
                        var parameters5Min = new DataRequestParameters(dateTimeFrom, dateTimeNow, 5, kvp.Value);
                        var parameterList = new List<DataRequestParameters>();
                        parameterList.Add(parameters5Min);
                        var dataPackageList = await dataLoader.GetPriceDataAsync(parameterList);
                        //var dataPackageList = task.Result;
                        if (dataPackageList[0].RowCount == 0)// || dataPackageList[1].RowCount == 0 || dataPackageList[2].RowCount == 0)
                        {
                            throw new Exception();
                        }
                        backtester.PriceDataPackage = dataPackageList;
                        Console.WriteLine("Data fetched for symbol " + kvp.Key);

                        Strategy.ADXBBStrategy testStrategy = new Strategy.ADXBBStrategy();
                        LiveSignalData prevSignal = signalGenerated;

                        signalGenerated = backtester.RunLiveSignalCheck(backtester.PriceDataPackage, testStrategy);
                        if (signalGenerated.entrySignal != Strategy.EntrySignal.NoSignal)
                        {
                            Console.WriteLine("Signal found at " + dateTimeNow.ToLocalTime());
                            Console.WriteLine("Symbol: " + kvp.Key);
                            Console.WriteLine("");
                            new ToastContentBuilder().AddArgument("key", "value")
                                                     .AddText("Signal found at " + dateTimeNow.ToLocalTime())
                                                     .AddText(kvp.Key + " " + signalGenerated.entrySignal + " at " + signalGenerated.triggerPrice)
                                                     .AddText("Stop loss at " + signalGenerated.stopLoss + ", take profit at " + signalGenerated.takeProfit)
                                                     .Show();

                            //simpleSound.Play();
                        }
                        else
                        {
                            Console.WriteLine("No signal found at " + dateTimeNow.ToLocalTime());
                            Console.WriteLine("Symbol: " + kvp.Key);
                            Console.WriteLine("");
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex);
                        Console.WriteLine("Data was empty, retrying in one second...");
                        Thread.Sleep(1000);
                    }
                    loopFailed = false;
                    
                }
               
                    
            }
            
        }
        
        public void stopLiveMonitoring()
        {
            timer.Enabled = false;
            timer.Dispose();
            Console.WriteLine("Stopped live monitoring.");
        }
    }

    public class LiveSignalData
    {
        public Strategy.EntrySignal entrySignal;
        public decimal triggerPrice;
        public decimal stopLoss;
        public decimal takeProfit;
        public LiveSignalData(Strategy.EntrySignal entrySignal, decimal triggerPrice, decimal stopLoss, decimal takeProfit)
        {
            this.entrySignal = entrySignal;
            this.triggerPrice = triggerPrice;
            this.stopLoss = stopLoss;
            this.takeProfit = takeProfit;
        }
        public LiveSignalData(Strategy.EntrySignal entrySignal)
        {
            this.entrySignal = entrySignal;
        }
    }
}
