using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Data;
using Newtonsoft.Json.Linq;
using Deedle;
using System.Net.WebSockets;
using System.Threading;
using System.IO;

namespace AutoTrader
{
    //this class should maybe just be a struct, and the encapsulation might be unnecessary too

    public class DataRequestParameters
    {
        //5000 candles is the maximum amount for a single request
        //resolution is expressed in minutes, but daily or monthly charts are requested with resolution = D or = M
        private DateTime fromDateTime;
        private DateTime toDateTime;
        private int resolution;
        private string symbolID;
        //add a parameter for symbol at some point?
        public DataRequestParameters(DateTime from, DateTime to, int timeFrame, string symbol)
        {
            FromDateTime = from;
            ToDateTime = to;
            Resolution = timeFrame;
            SymbolID = symbol;

        }

        public DateTime FromDateTime { get => fromDateTime; set => fromDateTime = value; }
        public DateTime ToDateTime { get => toDateTime; set => toDateTime = value; }
        public int Resolution { get => resolution; set => resolution = value; }
        public string SymbolID { get => symbolID; set => symbolID = value; }
    }

    public class DataLoader
    {

        HttpClient client = new HttpClient();


        public async Task<List<Frame<DateTime, string>>> GetPriceDataAsync(List<DataRequestParameters> parametersList, bool saveToDisk = false, string filePath = "data.csv")
        {

            List<Frame<DateTime, string>> priceDataPackage = new List<Frame<DateTime, string>>();
            foreach (DataRequestParameters parameterSet in parametersList)
            {

                long fromTime = CalcTest.DateTimeToUnix(parameterSet.FromDateTime);
                long toTime = CalcTest.DateTimeToUnix(parameterSet.ToDateTime);
                long timespanInMinutes = (toTime - fromTime) / 60;

                //If the fromTime is evenly divisible with the resolution of the data, the last row of each chunk will appear
                //again as the first row of the following chunk. This statement checks for this error.
                bool duplicateRowError = false;
                if ((timespanInMinutes * 60) % (parameterSet.Resolution * 60) == 0)
                {
                    duplicateRowError = true;
                }
                //if a request could potentially return more than 5000 candles, it is chunked into multiple requests of 5k candles max
                if (timespanInMinutes / parameterSet.Resolution >= 5000)
                {
                    int chunks = (int)(timespanInMinutes / parameterSet.Resolution) / 5000;
                    Frame<DateTime, string>[] tempArray = new Frame<DateTime, string>[chunks + 1];

                    //offset value is the second equivalent of 5000 candles
                    long offset = parameterSet.Resolution * 60 * 5000;
                    for (int i = 0; i < chunks; i++)
                    {

                        DateTime from = CalcTest.UnixTimeToDateTime(fromTime);
                        DateTime to = CalcTest.UnixTimeToDateTime(fromTime + offset);
                        fromTime = fromTime + offset;
                        DataRequestParameters chunkParameters = new(from, to, parameterSet.Resolution, parameterSet.SymbolID);
                        var looptask = GetJsonDataAsync(chunkParameters, duplicateRowError);
                        tempArray[i] = await looptask;

                        //tempArray[i].Print();
                        //tempArray[i].SaveCsv("joinedData" + i +".csv", true);

                    }
                    DateTime finalFrom = CalcTest.UnixTimeToDateTime(fromTime);
                    DataRequestParameters finalChunkParameters = new(finalFrom, parameterSet.ToDateTime, parameterSet.Resolution, parameterSet.SymbolID);
                    var finalTask = GetJsonDataAsync(finalChunkParameters);


                    try
                    {
                        tempArray[chunks] = await finalTask;
                    }
                    catch (NullReferenceException ex)
                    {
                        Console.WriteLine("Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message);
                        Console.WriteLine("Returning empty dataframe, requested chunk was probably empty.");

                        Frame<DateTime, string> emptyFrame = Frame.CreateEmpty<DateTime, string>();
                        tempArray[chunks] = emptyFrame;
                    }
                    //tempArray[chunks].SaveCsv("joinedData" + chunks + ".csv", true);
                    //tempArray[chunks].Print();
                    Frame<DateTime, string> tempDataFrame = Frame.CreateEmpty<DateTime, string>();
                    var joinedData = tempDataFrame.Merge(tempArray);

                    if (saveToDisk == true)
                    {
                        joinedData.SaveCsv(filePath, true);
                    }


                    //Console.WriteLine(joinedData.Format(100));
                    priceDataPackage.Add(joinedData);
                }
                else
                {
                    var task = GetJsonDataAsync(parameterSet);
                    var joinedData = await task;
                    priceDataPackage.Add(joinedData);

                    if (saveToDisk == true)
                    {
                        joinedData.SaveCsv(filePath, true);
                    }
                }

            }
            return priceDataPackage;
        }

        private async Task<Frame<DateTime, string>> GetJsonDataAsync(DataRequestParameters parameters, bool duplicateErrorCorrection = false)
        {
            string fromDateTimeStr = CalcTest.DateTimeToUnix(parameters.FromDateTime).ToString();
            string toDateTimeStr = CalcTest.DateTimeToUnix(parameters.ToDateTime).ToString();
            string resolution = parameters.Resolution.ToString();
            string symbol = parameters.SymbolID;
            //8874 = nasdaq fut
            //8826 = dax fut
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://tvc4.forexpros.com/b7e7d9236a1d4aac57061a105acbe7f4/1616863579/1/1/8/history?symbol=" + symbol + "&resolution=" + resolution + "&from=" + fromDateTimeStr + "&to=" + toDateTimeStr),
                Method = HttpMethod.Get,
                Headers =
                {
                    {"User-Agent", "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:81.0) Gecko/20100101 Firefox/81.0"},
                    {"Referer", "https://tvc-invdn-com.akamaized.net/" },

                }
            };
            Console.WriteLine("HttpRequestMessage created.");

            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";
            var priceQuoteList = new List<PriceQuote>();
            try
            {
                Console.WriteLine("Sending GET request...");
                //Send the GET request
                httpResponse = await client.SendAsync(request);
                Console.WriteLine("GET request sent.");
                httpResponse.EnsureSuccessStatusCode();
                Console.WriteLine("Code of httpResponse: " + httpResponse.StatusCode);
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();

                JObject o = JObject.Parse(httpResponseBody);

                JArray timestampJArray = (JArray)o.SelectToken("t");
                JArray closeJArray = (JArray)o.SelectToken("c");
                JArray openJArray = (JArray)o.SelectToken("o");
                JArray highJArray = (JArray)o.SelectToken("h");
                JArray lowJArray = (JArray)o.SelectToken("l");
                //try
                //{
                if (timestampJArray == null)
                {
                    Console.WriteLine("timestampJArray was null");

                    var emptyPriceQuotes = Frame.FromRecords(priceQuoteList).IndexRows<DateTime>("Timestamp");
                    return emptyPriceQuotes;
                }
                for (int i = 0; i < timestampJArray.Count; i++)
                {
                    priceQuoteList.Add(new PriceQuote
                    {
                        Timestamp = CalcTest.UnixTimeToDateTime((long)timestampJArray[i]),
                        Close = (decimal)closeJArray[i],
                        Open = (decimal)openJArray[i],
                        High = (decimal)highJArray[i],
                        Low = (decimal)lowJArray[i]
                    });
                }
                if (duplicateErrorCorrection == true)
                {
                    priceQuoteList.RemoveAt(priceQuoteList.Count - 1);
                }
                /*}
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message);
                }*/


                //Console.WriteLine(candlesJArray.ToString());

                Console.WriteLine("---");
                //Console.WriteLine("---");
                //Console.WriteLine("---");
                //Console.WriteLine("---");

            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }
            var dfPriceQuotes = Frame.FromRecords(priceQuoteList).IndexRows<DateTime>("Timestamp");
            //dfPriceQuotes.Print();

            return dfPriceQuotes;
        }

    }

    public class WsClient : IDisposable
    {

        public int ReceiveBufferSize { get; set; } = 8192;

        public async Task ConnectAsync(string url)
        {
            if (WS != null)
            {
                if (WS.State == WebSocketState.Open) return;
                else WS.Dispose();
            }
            WS = new ClientWebSocket();
            if (CTS != null) CTS.Dispose();
            CTS = new CancellationTokenSource();
            await WS.ConnectAsync(new Uri(url), CTS.Token);
            await Task.Factory.StartNew(ReceiveLoop, CTS.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public async Task DisconnectAsync()
        {
            if (WS is null) return;
            // TODO: requests cleanup code, sub-protocol dependent.
            if (WS.State == WebSocketState.Open)
            {
                CTS.CancelAfter(TimeSpan.FromSeconds(2));
                await WS.CloseOutputAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
                await WS.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            WS.Dispose();
            WS = null;
            CTS.Dispose();
            CTS = null;
        }

        private async Task ReceiveLoop()
        {
            var loopToken = CTS.Token;
            MemoryStream outputStream = null;
            WebSocketReceiveResult receiveResult = null;
            var buffer = new byte[ReceiveBufferSize];
            try
            {
                while (!loopToken.IsCancellationRequested)
                {
                    outputStream = new MemoryStream(ReceiveBufferSize);
                    do
                    {
                        receiveResult = await WS.ReceiveAsync(buffer, CTS.Token);
                        if (receiveResult.MessageType != WebSocketMessageType.Close)
                            outputStream.Write(buffer, 0, receiveResult.Count);
                    }
                    while (!receiveResult.EndOfMessage);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                        break;
                    outputStream.Position = 0;
                    ResponseReceived(outputStream);

                    Console.WriteLine("Mitä vittua tää ny on");
                    Console.WriteLine(outputStream);
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                outputStream?.Dispose();
            }
        }

        private async Task<string> SendMessageAsync<RequestType>(RequestType message)
        {
            return null;
            // TODO: handle serializing requests and deserializing responses, handle matching responses to the requests.
        }

        private void ResponseReceived(Stream inputStream)
        {
            // TODO: handle deserializing responses and matching them to the requests.
            // IMPORTANT: DON'T FORGET TO DISPOSE THE inputStream!
        }

        public void Dispose() => DisconnectAsync().Wait();

        private ClientWebSocket WS;
        private CancellationTokenSource CTS;

    }
}
