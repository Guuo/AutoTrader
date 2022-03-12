using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skender.Stock.Indicators;
using Deedle;
using tinet;

namespace AutoTrader
{

    public static class CalcTest
    {

        public static DateTime UnixTimeToDateTime(long unixTime)
        {
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dtDateTime = dtDateTime.AddSeconds(unixTime).ToLocalTime();
            return dtDateTime;
        }
        public static long DateTimeToUnix(DateTime MyDateTime)
        {
            TimeSpan timeSpan = MyDateTime - new DateTime(1970, 1, 1, 0, 0, 0);

            return (long)timeSpan.TotalSeconds;
        }

        public static void CalculateEMA(Frame<DateTime, string> dfPriceQuotes, int length)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            

            var priceIEnum = closePrices.Values;
            var priceList = priceIEnum.ToList();

            //get a sublist for the SMA seed calculation that the EMA calculation is based on
            List<decimal> sublist = priceList.GetRange(0, length);

            //SMA calculation, assigned to "latestEMA" because it is used as the initial value for EMA calculation,
            //and is therefore the first "EMA" value
            decimal latestEMA = sublist.Sum() / length;
            decimal smoothingConstant = 2M / ((decimal)length + 1M);

            var EMAList = new List<decimal>();

            //fill the beginning of the EMA list with the initial SMA value as to not leave missing values
            //mostly because it was simpler than handling an EMA list that has fewer entries than there are entries in the dataframe
            for(int i = 0; i < length; i++)
            {
                EMAList.Add(latestEMA);
            }

            decimal newEMA;
            //EMA calculation for each data point
            for (int i = length; i < priceList.Count; i++)
            {
                
                newEMA = (priceList[i] - latestEMA) * smoothingConstant + latestEMA;
                EMAList.Add(newEMA);
                latestEMA = newEMA;
            }
            
            dfPriceQuotes.ReplaceColumn("EMA " + length, EMAList);

            //Console.Write(dfPriceQuotes.Format(50, 50));
            //dfPriceQuotes.Print();
        }

        public static void CalculateSMA(Frame<DateTime, string> dfPriceQuotes, int length)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var priceIEnum = closePrices.Values;
            var priceList = priceIEnum.ToList();

            //get a sublist for the SMA calculation
            List<decimal> sublist = priceList.GetRange(0, length);

            //SMA calculation
            var SMAList = new List<decimal>();
            decimal SMA = sublist.Sum() / length;

            //Queue<decimal> buffer = new Queue<decimal>();
            for (int i = 0; i < length; i++)
            {
                //buffer.Enqueue(priceList[i]);
                SMAList.Add(SMA);
            }
            decimal SMAIncrement;
            for (int i = length; i < priceList.Count; i++)
            {
                //buffer.Dequeue();
                //buffer.Enqueue(priceList[i]);

                SMAIncrement = (priceList[i] - priceList[i - length]) / length;
                SMA = SMA + SMAIncrement;
                SMAList.Add(SMA);
            }
            
            dfPriceQuotes.ReplaceColumn("SMA " + length, SMAList);
            //dfPriceQuotes.Print();
        }

        public static void CalculateStandardDeviation(Frame<DateTime, string> dfPriceQuotes, int length)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var priceIEnum = closePrices.Values;
            var priceList = priceIEnum.ToList();
            List<decimal> sublist = priceList.GetRange(0, length);

            //initial stdev calculation
            //dSquared / length - 1 = sample variance
            decimal mean = 0.0M;
            decimal dSquared = 0.0M;
            int k = 1;
            foreach (decimal price in sublist)
            {
                decimal prevMean = mean;
                
                mean += (price - prevMean) / k;
                dSquared += (price - prevMean) * (price - mean);
                k++;
            }
            Console.WriteLine("Lopputulos eli STDev = " + (decimal)Math.Sqrt((double)(dSquared / (k - 2))));
            decimal stdev = (decimal)Math.Sqrt((double)(dSquared / (k - 2)));
            List<decimal> stdevList = new();
            for (int i = 0; i < length; i++)
            {
                //buffer.Enqueue(priceList[i]);
                stdevList.Add(stdev);
            }

            decimal dSquaredIncrement;
            decimal meanIncrement;
            
            for (int i = length; i < priceList.Count; i++)
            {
                decimal prevMean = mean;

                meanIncrement = (priceList[i] - priceList[i - length]) / length;
                mean = mean + meanIncrement;
                dSquaredIncrement = ((priceList[i] - priceList[i - length]) * (priceList[i] - mean + priceList[i - length] - prevMean));
                dSquared = dSquared + dSquaredIncrement;
                stdevList.Add((decimal)Math.Sqrt((double)(dSquared / (length - 1))));
            }
            
            dfPriceQuotes.ReplaceColumn("STDev " + length, stdevList);
            dfPriceQuotes.Print();
            //The value this gives is basically a Bollinger Band of 1 standard deviation, meaning
            //STDev + SMA 20 = upper BB (20, 1)
            //This STDev however, is calculated as sample STDev unlike BB which is usually calculated as a population STDev
        }

        public static void CalculateCoppockCurve(Frame<DateTime, string> dfPriceQuotes, int WMALength, int longRoCLength, int shortRoCLength)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalPriceArray = closePrices.Values.ToArray();
            double[] priceArray = Array.ConvertAll(decimalPriceArray, new Converter<decimal, double>(Decimal.ToDouble));


            int outputLength;
            double[][] inputs = { priceArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { longRoCLength };
            outputLength = priceArray.Length - indicators.roc.start(options);

            double[] longRoCArray = new double[outputLength];
            double[][] outputs = { longRoCArray };
            int success = tinet.indicators.roc.run(inputs, options, outputs);
            //Console.WriteLine(LongRoCoutput[LongRoCoutput.Length - 1]);

            options[0] = shortRoCLength;
            outputLength = priceArray.Length - indicators.roc.start(options);
            //Console.WriteLine("priceArray length: " + priceArray.Length);

            double[] shortRoCArray = new double[outputLength];
            outputs[0] = shortRoCArray;
            success = tinet.indicators.roc.run(inputs, options, outputs);
            

            //LongRoC + ShortRoC
            double[] combinedRoC = new double[longRoCArray.Length];
            for(int i = 0; i < combinedRoC.Length; i++)
            {
                combinedRoC[i] = longRoCArray[i] + shortRoCArray[i + longRoCLength - shortRoCLength];
            }

            options[0] = WMALength;
            outputLength = combinedRoC.Length - indicators.wma.start(options);

            double[] WMAoutput = new double[outputLength];
            outputs[0] = WMAoutput;
            inputs[0] = combinedRoC;
            success = tinet.indicators.wma.run(inputs, options, outputs);

            //Console.WriteLine("CombinedRoC length: " + CombinedRoC.Length);
            //Console.WriteLine("Coppock curve length: " + outputLength);
            //Console.WriteLine("Price values count: " + priceArray.Length);
            //Console.WriteLine("Coppock curve length " + WMAoutput.Length);
            double[] coppockValues = new double[priceArray.Length];

            int offset = priceArray.Length - WMAoutput.Length;
            for (int i = 0; i < offset; i++)
            {
                coppockValues[i] = 0;
            }
           
            for(int i = offset; i < coppockValues.Length; i++)
            {
                coppockValues[i] = WMAoutput[i - offset] * 100;
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("Coppock curve", coppockValues);  
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }


            //dfPriceQuotes.Print();

        }

        public static void CalculateAroon(Frame<DateTime, string> dfPriceQuotes, int WMALength, int longRoCLength, int shortRoCLength)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalPriceArray = closePrices.Values.ToArray();
            double[] priceArray = Array.ConvertAll(decimalPriceArray, new Converter<decimal, double>(Decimal.ToDouble));
        }
        public static void CalculateLinRegCurve(Frame<DateTime, string> dfPriceQuotes, int linRegLength)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalPriceArray = closePrices.Values.ToArray();
            double[] priceArray = Array.ConvertAll(decimalPriceArray, new Converter<decimal, double>(Decimal.ToDouble));

            int outputLength;
            double[][] inputs = { priceArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { linRegLength };
            outputLength = priceArray.Length - indicators.linreg.start(options);

            double[] linRegArray = new double[outputLength];
            double[][] outputs = { linRegArray };
            int success = tinet.indicators.linreg.run(inputs, options, outputs);



            double[] linRegValues = new double[priceArray.Length];

            int offset = priceArray.Length - linRegArray.Length;
            for (int i = 0; i < offset; i++)
            {
                linRegValues[i] = 0;
            }

            for (int i = offset; i < linRegValues.Length; i++)
            {
                linRegValues[i] = linRegArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("LinReg curve", linRegValues);
                //dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }

        //CURRENTLY ALLOWS FOR ONLY 1 HMA COLUMN PER FRAME DUE TO COLUMN NAMING
        public static void CalculateHMA(Frame<DateTime, string> dfPriceQuotes, int HMALength)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalPriceArray = closePrices.Values.ToArray();
            double[] priceArray = Array.ConvertAll(decimalPriceArray, new Converter<decimal, double>(Decimal.ToDouble));

            int outputLength;
            double[][] inputs = { priceArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { HMALength };
            outputLength = priceArray.Length - indicators.hma.start(options);

            double[] HMAArray = new double[outputLength];
            double[][] outputs = { HMAArray };
            int success = tinet.indicators.hma.run(inputs, options, outputs);



            double[] HMAValues = new double[priceArray.Length];

            int offset = priceArray.Length - HMAArray.Length;
            for (int i = 0; i < offset; i++)
            {
                HMAValues[i] = 0;
            }

            for (int i = offset; i < HMAValues.Length; i++)
            {
                HMAValues[i] = HMAArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("HMA " + HMALength, HMAValues);
                //dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }

        public static void CalculateSuperTrend(Frame<DateTime, string> dfPriceQuotes, int length, int multiplier)
        {
            var highPrices = dfPriceQuotes.GetColumn<decimal>("High");
            var lowPrices = dfPriceQuotes.GetColumn<decimal>("Low");
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalCloseArray = closePrices.Values.ToArray();
            var decimalHighArray = highPrices.Values.ToArray();
            var decimalLowArray = lowPrices.Values.ToArray();
            double[] highArray = Array.ConvertAll(decimalHighArray, new Converter<decimal, double>(Decimal.ToDouble));
            double[] lowArray = Array.ConvertAll(decimalLowArray, new Converter<decimal, double>(Decimal.ToDouble));
            double[] closeArray = Array.ConvertAll(decimalCloseArray, new Converter<decimal, double>(Decimal.ToDouble));


            int outputLength;
            double[][] inputs = { highArray, lowArray, closeArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { length };
            outputLength = closeArray.Length - indicators.atr.start(options);

            double[] ATRArray = new double[outputLength];
            double[][] outputs = { ATRArray };
            int success = tinet.indicators.atr.run(inputs, options, outputs);

            //ATRArray now holds our ATR values, time to start calculating SuperTrend

            double[] SuperTrendValues = new double[closeArray.Length];

            int offset = closeArray.Length - ATRArray.Length;
            for (int i = 0; i < offset; i++)
            {
                SuperTrendValues[i] = 0;
            }

            double basicUpper;
            double basicLower;
            double finalUpper = 0;
            double finalLower = 0;
            double prevFinalUpper = 0;
            double prevFinalLower = 0;
            for (int i = offset; i < SuperTrendValues.Length; i++)
            {
                basicUpper = (highArray[i] + lowArray[i]) / 2 + multiplier * ATRArray[i - offset];
                basicLower = (highArray[i] + lowArray[i]) / 2 - multiplier * ATRArray[i - offset];

                if (basicUpper < finalUpper || closeArray[i - 1] > finalUpper)
                    finalUpper = basicUpper;
                //No need for an else statement, it would simply be finalUpper = finalUpper which is redundant

                if (basicLower > finalLower | closeArray[i - 1] < finalLower)
                    finalLower = basicLower;

                if (SuperTrendValues[i - 1] == prevFinalUpper && closeArray[i] <= finalUpper)
                    SuperTrendValues[i] = finalUpper;
                else if (SuperTrendValues[i - 1] == prevFinalUpper && closeArray[i] > finalUpper)
                    SuperTrendValues[i] = finalLower;
                else if (SuperTrendValues[i - 1] == prevFinalLower && closeArray[i] >= finalLower)
                    SuperTrendValues[i] = finalLower;
                else if (SuperTrendValues[i - 1] == prevFinalLower && closeArray[i] < finalLower)
                    SuperTrendValues[i] = finalUpper;

                prevFinalUpper = finalUpper;
                prevFinalLower = finalLower;
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("SuperTrend " + length + ", " + multiplier, SuperTrendValues);
                //dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }

        public static void CalculateStochRSI(Frame<DateTime, string> dfPriceQuotes, int length, int k, int d)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalCloseArray = closePrices.Values.ToArray();
            double[] closeArray = Array.ConvertAll(decimalCloseArray, new Converter<decimal, double>(Decimal.ToDouble));

            int outputLength;
            double[][] RSIinputs = { closeArray };

            //Console.Write(output[output.Length - 1]);

            double[] RSIoptions = { length };
            int RSIOutputOffset = indicators.rsi.start(RSIoptions);
            outputLength = closeArray.Length - RSIOutputOffset;

            double[] RSIArray = new double[outputLength];
            double[][] RSIoutputs = { RSIArray };
            int success = tinet.indicators.rsi.run(RSIinputs, RSIoptions, RSIoutputs);

            //RSI is calculated, time to calculate Stoch RSI
            double[][] stochInputs = { RSIArray, RSIArray, RSIArray };
            double[] stochOptions = { length, k, d };
            outputLength = closeArray.Length - RSIOutputOffset - indicators.stoch.start(stochOptions);

            double[] stochKArray = new double[outputLength];
            double[] stochDArray = new double[outputLength];
            double[][] stochOutputs = { stochKArray, stochDArray };
            success = tinet.indicators.stoch.run(stochInputs, stochOptions, stochOutputs);

            double[] stochKValues = new double[closeArray.Length];
            double[] stochDValues = new double[closeArray.Length];

            int offset = closeArray.Length - stochKArray.Length;
            for (int i = 0; i < offset; i++)
            {
                stochKValues[i] = 0;
                stochDValues[i] = 0;
            }

            for (int i = offset; i < stochKValues.Length; i++)
            {
                stochKValues[i] = stochKArray[i - offset];
                stochDValues[i] = stochDArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("Stoch RSI k " + length, stochKValues);
                dfPriceQuotes.ReplaceColumn("Stoch RSI d " + length, stochDValues);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }


            //dfPriceQuotes.Print();

        }

        public static void CalculateATR(Frame<DateTime, string> dfPriceQuotes, int length)
        {
            var highPrices = dfPriceQuotes.GetColumn<decimal>("High");
            var lowPrices = dfPriceQuotes.GetColumn<decimal>("Low");
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalCloseArray = closePrices.Values.ToArray();
            var decimalHighArray = highPrices.Values.ToArray();
            var decimalLowArray = lowPrices.Values.ToArray();
            double[] highArray = Array.ConvertAll(decimalHighArray, new Converter<decimal, double>(Decimal.ToDouble));
            double[] lowArray = Array.ConvertAll(decimalLowArray, new Converter<decimal, double>(Decimal.ToDouble));
            double[] closeArray = Array.ConvertAll(decimalCloseArray, new Converter<decimal, double>(Decimal.ToDouble));


            int outputLength;
            double[][] inputs = { highArray, lowArray, closeArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { length };
            outputLength = closeArray.Length - indicators.atr.start(options);

            double[] ATRArray = new double[outputLength];
            double[][] outputs = { ATRArray };
            int success = tinet.indicators.atr.run(inputs, options, outputs);

            double[] ATRValues = new double[closeArray.Length];
            int offset = closeArray.Length - ATRArray.Length;
            for (int i = 0; i < offset; i++)
            {
                ATRValues[i] = 0;
            }

            for (int i = offset; i < ATRValues.Length; i++)
            {
                ATRValues[i] = ATRArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("ATR " + length, ATRValues);
                //dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }

        public static void CalculateWaveTrend(Frame<DateTime, string> dfPriceQuotes, int channelLength, int averageLength)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var highPrices = dfPriceQuotes.GetColumn<decimal>("High");
            var lowPrices = dfPriceQuotes.GetColumn<decimal>("Low");
            var decimalClose = closePrices.Values.ToArray();
            var decimalHigh = highPrices.Values.ToArray();
            var decimalLow = lowPrices.Values.ToArray();
            double[] highArray = Array.ConvertAll(decimalHigh, new Converter<decimal, double>(Decimal.ToDouble));
            double[] lowArray = Array.ConvertAll(decimalLow, new Converter<decimal, double>(Decimal.ToDouble));
            double[] closeArray = Array.ConvertAll(decimalClose, new Converter<decimal, double>(Decimal.ToDouble));

            /*
             * ap = hlc3 
            esa = ema(ap, n1)
            d = ema(abs(ap - esa), n1)
            ci = (ap - esa) / (0.015 * d)
            tci = ema(ci, n2)
 
            wt1 = tci
            wt2 = sma(wt1,4)

            no idea what most of these are short for
            doesn't matter, did math
            */

            //ap = hlc3
            List<double> avgPrice = new List<double>();
            for (int i = 0; i < closeArray.Length; i++)
            {
                avgPrice.Add((highArray[i] + lowArray[i] + closeArray[i]) / 3);
            }
            // esa = ema(ap, n1)
            int outputLength;
            double[][] ESAinputs = { avgPrice.ToArray() };

            double[] ESAoptions = { channelLength };
            outputLength = avgPrice.Count - indicators.ema.start(ESAoptions);

            double[] ESAArray = new double[outputLength];
            double[][] ESAoutputs = { ESAArray };
            int success = tinet.indicators.ema.run(ESAinputs, ESAoptions, ESAoutputs);

            //d = ema(abs(ap - esa), n1)
            var absValueList = new List<double>();
            int loopOffset1 = avgPrice.Count - ESAArray.Length;
            for (int i = 0; i < ESAArray.Length; i++)
            {
                absValueList.Add(Math.Abs(avgPrice[i + loopOffset1] - ESAArray[i]));
            }
            double[][] dInputs = { absValueList.ToArray() };

            double[] dOptions = { channelLength };
            outputLength = absValueList.Count - indicators.ema.start(dOptions);
            double[] dArray = new double[outputLength];
            double[][] dOutputs = { dArray };
            success = tinet.indicators.ema.run(dInputs, dOptions, dOutputs);

            //ci = (ap - esa) / (0.015 * d)
            double[] ciArray = new double[ESAArray.Length];
            loopOffset1 = avgPrice.Count - dArray.Length;
            int loopOffset2 = ESAArray.Length - dArray.Length;

            //skip first indexes to avoid possible NaN values and divide by zeroes that can be caused by absValueList having 0 as value
            //literally the laziest solution, just hoping that zeroes don't occur after the first 3 indexes
            for (int i = 3; i < dArray.Length; i++)
            {
                ciArray[i] = (avgPrice[i + loopOffset1] - ESAArray[i + loopOffset2]) / (0.015 * dArray[i]);
            }

            //tci = ema(ci, n2)
            //wt1 = tci
            double[][] tciInputs = { ciArray };

            double[] tciOptions = { averageLength };
            outputLength = ciArray.Length - indicators.ema.start(tciOptions);
            double[] tciArray = new double[outputLength];
            double[][] tciOutputs = { tciArray };
            success = tinet.indicators.ema.run(tciInputs, tciOptions, tciOutputs);

            //wt2 = sma(wt1,3)
            //SMA length could be a variable exposed to the user too, if needed or wanted
            double[][] wt2inputs = { tciArray };

            double[] wt2options = { 3 };
            outputLength = tciArray.Length - indicators.sma.start(wt2options);
            double[] waveTrend2Array = new double[outputLength];
            double[][] wt2outputs = { waveTrend2Array };
            success = tinet.indicators.sma.run(wt2inputs, wt2options, wt2outputs);

            var wt1List = new List<double>();
            var wt2List = new List<double>();

            for(int i = 0; i < closeArray.Length - waveTrend2Array.Length; i++)
            {
                wt1List.Add(0);
            }
            for (int i = 0; i < closeArray.Length - waveTrend2Array.Length; i++)
            {
                wt2List.Add(0);
            }
            for (int i = tciArray.Length - waveTrend2Array.Length; i < tciArray.Length; i++)
            {
                wt1List.Add(tciArray[i]);
            }
            for (int i = 0; i < waveTrend2Array.Length; i++)
            {
                wt2List.Add(waveTrend2Array[i]);
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("WaveTrend1", wt1List);
                dfPriceQuotes.ReplaceColumn("WaveTrend2", wt2List);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }


            //dfPriceQuotes.Print();

        }

        public static void CalculateVIXFIX(Frame<DateTime, string> dfPriceQuotes, int channelLength, int averageLength)
        {

        }

        public static void CalculateBBands(Frame<DateTime, string> dfPriceQuotes, int length, double mult)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalCloseArray = closePrices.Values.ToArray();
            double[] closeArray = Array.ConvertAll(decimalCloseArray, new Converter<decimal, double>(Decimal.ToDouble));

            int outputLength;
            double[][] inputs = { closeArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { length, mult };
            outputLength = closeArray.Length - indicators.bbands.start(options);

            double[] LowerBandArray = new double[outputLength];
            double[] MiddleBandArray = new double[outputLength];
            double[] UpperBandArray = new double[outputLength];
            double[][] outputs = { LowerBandArray, MiddleBandArray, UpperBandArray };
            int success = tinet.indicators.bbands.run(inputs, options, outputs);

            double[] LowerBandValues = new double[closeArray.Length];
            double[] MiddleBandValues = new double[closeArray.Length];
            double[] UpperBandValues = new double[closeArray.Length];
            int offset = closeArray.Length - MiddleBandArray.Length;

            for (int i = 0; i < offset; i++)
            {
                LowerBandValues[i] = 0;
                MiddleBandValues[i] = 0;
                UpperBandValues[i] = 0;
            }

            for (int i = offset; i < LowerBandValues.Length; i++)
            {
                LowerBandValues[i] = LowerBandArray[i - offset];
                MiddleBandValues[i] = MiddleBandArray[i - offset];
                UpperBandValues[i] = UpperBandArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("Lower BBand " + length + ", " + mult, LowerBandValues);
                dfPriceQuotes.ReplaceColumn("Middle BBand " + length + ", " + mult, MiddleBandValues);
                dfPriceQuotes.ReplaceColumn("Upper BBand " + length + ", " + mult, UpperBandValues);
                //dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }

        public static void CalculateMACD(Frame<DateTime, string> dfPriceQuotes, int fastLength, int slowLength, int signalLength)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalCloseArray = closePrices.Values.ToArray();
            double[] closeArray = Array.ConvertAll(decimalCloseArray, new Converter<decimal, double>(Decimal.ToDouble));

            int outputLength;
            double[][] inputs = { closeArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { fastLength, slowLength, signalLength };
            outputLength = closeArray.Length - indicators.macd.start(options);

            double[] fastArray = new double[outputLength];
            double[] slowArray = new double[outputLength];
            double[] histogramArray = new double[outputLength];
            double[][] outputs = { fastArray, slowArray, histogramArray };
            int success = tinet.indicators.macd.run(inputs, options, outputs);

            double[] fastValues = new double[closeArray.Length];
            double[] slowValues = new double[closeArray.Length];
            double[] histogramValues = new double[closeArray.Length];
            int offset = closeArray.Length - slowArray.Length;

            for (int i = 0; i < offset; i++)
            {
                fastValues[i] = 0;
                slowValues[i] = 0;
                histogramValues[i] = 0;
            }

            for (int i = offset; i < fastValues.Length; i++)
            {
                fastValues[i] = fastArray[i - offset];
                slowValues[i] = slowArray[i - offset];
                histogramValues[i] = histogramArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("MACD " + fastLength + ", " + slowLength + ", " + signalLength, fastValues);
                dfPriceQuotes.ReplaceColumn("MACD Signal " + fastLength + ", " + slowLength + ", " + signalLength, slowValues);
                dfPriceQuotes.ReplaceColumn("MACD Histogram " + fastLength + ", " + slowLength + ", " + signalLength, histogramValues);
                //dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }

        public static void CalculateRSI(Frame<DateTime, string> dfPriceQuotes, int length)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var decimalCloseArray = closePrices.Values.ToArray();
            double[] closeArray = Array.ConvertAll(decimalCloseArray, new Converter<decimal, double>(Decimal.ToDouble));

            int outputLength;
            double[][] inputs = { closeArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { length };
            outputLength = closeArray.Length - indicators.rsi.start(options);

            double[] rsiArray = new double[outputLength];
            double[][] outputs = { rsiArray };
            int success = tinet.indicators.rsi.run(inputs, options, outputs);

            double[] rsiValues = new double[closeArray.Length];
            int offset = closeArray.Length - rsiArray.Length;

            for (int i = 0; i < offset; i++)
            {

                rsiValues[i] = 0;
            }

            for (int i = offset; i < rsiValues.Length; i++)
            {
                rsiValues[i] = rsiArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("RSI " + length, rsiValues);
                dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }

        public static void CalculateRSIDivergence(Frame<DateTime, string> dfPriceQuotes, string securityColumn, string indicatorColumn)
        {
            var priceValues = dfPriceQuotes.GetColumn<decimal>(securityColumn);
            var decimalCloseArray = priceValues.Values.ToArray();
            double[] priceArray = Array.ConvertAll(decimalCloseArray, new Converter<decimal, double>(Decimal.ToDouble));

            var indicatorValues = dfPriceQuotes.GetColumn<double>(indicatorColumn);
            var indicatorValueArray = indicatorValues.Values.ToArray();

            double[][] donchianChannels = CalculateDonchianChannels(priceArray, indicatorValueArray, 20);

            double[] priceMinValues = donchianChannels[0];
            double[] priceMaxValues = donchianChannels[1];
            double[] indicatorMinValues = donchianChannels[2];
            double[] indicatorMaxValues = donchianChannels[3];

            double priceA = 0;
            double priceB = 0;
            double indicatorA = 0;
            double indicatorB = 0;
            //finding the divergences
            //A is the most recent value, B is the previous high/low
            double[] bullDiv = new double[priceArray.Length];
            double[] bearDiv = new double[priceArray.Length];

            for (int i = 1; i < priceArray.Length; i++)
            {
                //bearish divergence
                priceA = priceMaxValues[i];
                priceB = priceMaxValues[i - 1];
                if(priceA > priceB)
                {
                    indicatorA = indicatorMaxValues[i];
                    indicatorB = indicatorMaxValues[i - 1];
                    if(indicatorA <= indicatorB && indicatorB > 70)
                    {
                        bearDiv[i] = 1;
                    }
                }

                //bullish divergence
                priceA = priceMinValues[i];
                priceB = priceMinValues[i - 1];
                if (priceA < priceB)
                {
                    indicatorA = indicatorMinValues[i];
                    indicatorB = indicatorMinValues[i - 1];
                    if (indicatorA >= indicatorB && indicatorB < 30)
                    {
                        bullDiv[i] = 1;
                    }
                }
            }

            try
            {
                dfPriceQuotes.ReplaceColumn(indicatorColumn + " bulldiv", bullDiv );
                dfPriceQuotes.ReplaceColumn(indicatorColumn + " beardiv", bearDiv);
                //dfPriceQuotes.ReplaceColumn("priceMin", priceMinValues);
                //dfPriceQuotes.ReplaceColumn("priceMax", priceMaxValues);
                //dfPriceQuotes.ReplaceColumn("indicatorMin", indicatorMinValues);
                //dfPriceQuotes.ReplaceColumn("indicatorMax", indicatorMaxValues);
                //Console.WriteLine(dfPriceQuotes.Format(500));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }
        //returned arrays are ordered as follows: priceMin, priceMax, indicatorMin, indicatorMax
        private static double[][] CalculateDonchianChannels(double[] priceArray, double[] indicatorValueArray, int length)
        {
            int outputLength;
            double[][] inputs = { priceArray };

            //Console.Write(output[output.Length - 1]);

            //pricemin
            double[] options = { length };
            outputLength = priceArray.Length - indicators.min.start(options);

            double[] priceMinArray = new double[outputLength];
            double[][] outputs = { priceMinArray };
            int success = tinet.indicators.min.run(inputs, options, outputs);

            double[] priceMinValues = new double[priceArray.Length];
            int offset = priceArray.Length - priceMinArray.Length;

            //pricemax
            outputLength = priceArray.Length - indicators.max.start(options);

            double[] priceMaxArray = new double[outputLength];
            outputs[0] = priceMaxArray;
            success = tinet.indicators.max.run(inputs, options, outputs);

            double[] priceMaxValues = new double[priceArray.Length];
            offset = priceArray.Length - priceMaxArray.Length;

            for (int i = 0; i < offset; i++)
            {
                priceMinValues[i] = 0;
                priceMaxValues[i] = 0;
            }

            for (int i = offset; i < priceMinValues.Length; i++)
            {
                priceMinValues[i] = priceMinArray[i - offset];
                priceMaxValues[i] = priceMaxArray[i - offset];
            }

            //indicatormin
            inputs[0] = indicatorValueArray;
            outputLength = indicatorValueArray.Length - indicators.min.start(options);

            double[] indicatorMinArray = new double[outputLength];
            outputs[0] = indicatorMinArray;
            success = tinet.indicators.min.run(inputs, options, outputs);

            double[] indicatorMinValues = new double[priceArray.Length];
            offset = priceArray.Length - indicatorMinArray.Length;

            //indicatormax
            outputLength = indicatorValueArray.Length - indicators.max.start(options);

            double[] indicatorMaxArray = new double[outputLength];
            outputs[0] = indicatorMaxArray;
            success = tinet.indicators.max.run(inputs, options, outputs);

            double[] indicatorMaxValues = new double[priceArray.Length];
            offset = priceArray.Length - indicatorMaxArray.Length;

            for (int i = 0; i < offset; i++)
            {
                indicatorMinValues[i] = 0;
                indicatorMaxValues[i] = 0;
            }

            for (int i = offset; i < indicatorMinValues.Length; i++)
            {
                indicatorMinValues[i] = indicatorMinArray[i - offset];
                indicatorMaxValues[i] = indicatorMaxArray[i - offset];
            }
            double[][] donchianChannel = new double[4][];
            donchianChannel[0] = priceMinValues;
            donchianChannel[1] = priceMaxValues;
            donchianChannel[2] = indicatorMinValues;
            donchianChannel[3] = indicatorMaxValues;

            return donchianChannel;
        }
        public static void CalculateADX(Frame<DateTime, string> dfPriceQuotes, int period)
        {
            var closePrices = dfPriceQuotes.GetColumn<decimal>("Close");
            var highPrices = dfPriceQuotes.GetColumn<decimal>("High");
            var lowPrices = dfPriceQuotes.GetColumn<decimal>("Low");
            var decimalCloseArray = closePrices.Values.ToArray();
            var decimalHighArray = highPrices.Values.ToArray();
            var decimalLowArray = lowPrices.Values.ToArray();
            double[] closeArray = Array.ConvertAll(decimalCloseArray, new Converter<decimal, double>(Decimal.ToDouble));
            double[] highArray = Array.ConvertAll(decimalHighArray, new Converter<decimal, double>(Decimal.ToDouble));
            double[] lowArray = Array.ConvertAll(decimalLowArray, new Converter<decimal, double>(Decimal.ToDouble));


            int outputLength;
            double[][] inputs = { highArray, lowArray, closeArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = { period };
            outputLength = closeArray.Length - indicators.adx.start(options);

            double[] ADXArray = new double[outputLength];
            double[][] outputs = {  ADXArray };
            int success = tinet.indicators.adx.run(inputs, options, outputs);

            double[] ADXValues = new double[closeArray.Length];
            int offset = closeArray.Length - ADXArray.Length;

            for (int i = 0; i < offset; i++)
            {
                ADXValues[i] = 0;
            }

            for (int i = offset; i < ADXValues.Length; i++)
            { 
                ADXValues[i] = ADXArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("ADX " + period, ADXValues);
                //dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }
        public static void CalculateAO(Frame<DateTime, string> dfPriceQuotes)
        {
            var highPrices = dfPriceQuotes.GetColumn<decimal>("High");
            var lowPrices = dfPriceQuotes.GetColumn<decimal>("Low");
            var decimalHighArray = highPrices.Values.ToArray();
            var decimalLowArray = lowPrices.Values.ToArray();
            double[] highArray = Array.ConvertAll(decimalHighArray, new Converter<decimal, double>(Decimal.ToDouble));
            double[] lowArray = Array.ConvertAll(decimalLowArray, new Converter<decimal, double>(Decimal.ToDouble));


            int outputLength;
            double[][] inputs = { highArray, lowArray };

            //Console.Write(output[output.Length - 1]);

            double[] options = {  };
            outputLength = highArray.Length - indicators.ao.start(options);

            double[] AOArray = new double[outputLength];
            double[][] outputs = { AOArray };
            int success = tinet.indicators.ao.run(inputs, options, outputs);

            double[] AOValues = new double[highArray.Length];
            int offset = highArray.Length - AOArray.Length;

            for (int i = 0; i < offset; i++)
            {
                AOValues[i] = 0;
            }

            for (int i = offset; i < AOValues.Length; i++)
            {
                AOValues[i] = AOArray[i - offset];
            }

            try
            {
                dfPriceQuotes.ReplaceColumn("AO", AOValues);
                //dfPriceQuotes.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error GET :D");
                Console.WriteLine(ex);

                dfPriceQuotes.Print();
                Console.WriteLine("What wrong? :D");

            }
        }


    }

    public class PriceQuote
    {
        private DateTime timestamp;
        private decimal close;
        private decimal open;
        private decimal high;
        private decimal low;

        public decimal Close { get => close; set => close = value; }
        public decimal Open { get => open; set => open = value; }
        public decimal High { get => high; set => high = value; }
        public decimal Low { get => low; set => low = value; }
        public DateTime Timestamp { get => timestamp; set => timestamp = value; }
    }
}
