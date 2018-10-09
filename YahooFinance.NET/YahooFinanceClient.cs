using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace YahooFinance.NET
{
	public class YahooFinanceClient
	{
		private const string BaseUrl = "https://query1.finance.yahoo.com/";
		private const string BasePath = "v7/finance/download/";
		private const string RealTimeUrl = "http://finance.yahoo.com/d/quotes.csv?s=";
		private const string RealTimeSuffix = "&f=abl1pohgt1nsv";
        private const string GetCookieCrumbURL = "https://finance.yahoo.com/quote/";
        private const string GetCookieCrumbPath = "%5EGSPC/history?p=^GSPC";

        public string Cookie { get; private set; }
        public string Crumb { get; private set; }

       private enum HistoryType
		{
			DividendHistory = 1,
			Day,
			Week,
			Month,
		}

		public YahooFinanceClient(string cookie, string crumb)
		{
			Cookie = cookie;
			Crumb = crumb;
		}

        public YahooFinanceClient()
        {
            Cookie = string.Empty;
            Crumb = string.Empty;
        }

        public void RefreshCookieAndCrumb()
        {
            var baseAddress = new Uri(GetCookieCrumbURL);
            var cookieContainer = new CookieContainer();

            using (var handler = new HttpClientHandler() { UseCookies = true, CookieContainer = cookieContainer })
            {
                using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
                {
                    using (var response = client.GetAsync(GetCookieCrumbPath).Result)
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            //find crumb object
                            var result = response.Content.ReadAsStringAsync().Result;
                            var crumbLoc = result.LastIndexOf("\"crumb\"");
                            var deviceLoc = result.LastIndexOf("\"device\"");
                            Crumb = result.Substring(crumbLoc, deviceLoc - crumbLoc - 2).Remove(0, 9);

                            //get cookie objects
                            bool found = false;
                            foreach (DictionaryEntry element in (Hashtable)cookieContainer.GetType().GetField("m_domainTable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cookieContainer))
                            {
                                if (element.Key.Equals(".yahoo.com"))
                                {
                                    SortedList cookieList = (SortedList)element.Value.GetType().GetField("m_list", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(element.Value);
                                    foreach (DictionaryEntry cookieitems in cookieList)
                                    {
                                        var cookies = (CookieCollection)cookieitems.Value;
                                        foreach (Cookie cookieItem in cookies)
                                        {
                                            Cookie = cookieItem.Value.Substring(0, cookieItem.Value.IndexOf("&"));
                                            found = true;
                                            break;
                                        }
                                        if (found)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public string GetYahooStockCode(string exchange, string code)
		{
			var exchangeHelper = new YahooExchangeHelper();
			return exchangeHelper.GetYahooStockCode(exchange, code);
		}

		public List<YahooHistoricalPriceData> GetDailyHistoricalPriceData(string yahooStockCode, DateTime? startDate = null,
			DateTime? endDate = null)
		{
			return GetHistoricalPriceData(yahooStockCode, HistoryType.Day, startDate, endDate);
		}

		public List<YahooHistoricalPriceData> GetWeeklyHistoricalPriceData(string yahooStockCode, DateTime? startDate = null,
			DateTime? endDate = null)
		{
			return GetHistoricalPriceData(yahooStockCode, HistoryType.Week, startDate, endDate);
		}

		public List<YahooHistoricalPriceData> GetMonthlyHistoricalPriceData(string yahooStockCode, DateTime? startDate = null, DateTime? endDate = null)
		{
			return GetHistoricalPriceData(yahooStockCode, HistoryType.Month, startDate, endDate);
		}

		public List<YahooHistoricalDividendData> GetHistoricalDividendData(string yahooStockCode, DateTime? startDate = null, DateTime? endDate = null)
		{
			var dividendHistoryCsv = GetHistoricalDataAsCsv(yahooStockCode, HistoryType.DividendHistory, startDate, endDate);

			var historicalDevidendData = new List<YahooHistoricalDividendData>();
			foreach (var line in dividendHistoryCsv.Split('\n').Skip(1))
			{
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}

				var values = line.Split(',');

				var newDividendData = new YahooHistoricalDividendData
				{
					Date = DateTime.Parse(values[0], CultureInfo.InvariantCulture),
					Dividend = decimal.Parse(values[1], CultureInfo.InvariantCulture),
				};
				historicalDevidendData.Add(newDividendData);
			}

			return historicalDevidendData;
		}

		private List<YahooHistoricalPriceData> GetHistoricalPriceData(string yahooStockCode, HistoryType historyType, DateTime? startDate, DateTime? endDate)
		{
			var historicalDataCsv = GetHistoricalDataAsCsv(yahooStockCode, historyType, startDate, endDate);

			var historicalPriceData = new List<YahooHistoricalPriceData>();
			foreach (var line in historicalDataCsv.Split('\n').Skip(1))
			{
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}

				var values = line.Split(',');

				var newPriceData = new YahooHistoricalPriceData
				{
					Date = DateTime.Parse(values[0], CultureInfo.InvariantCulture),
					Open = decimal.Parse(values[1], CultureInfo.InvariantCulture),
					High = decimal.Parse(values[2], CultureInfo.InvariantCulture),
					Low = decimal.Parse(values[3], CultureInfo.InvariantCulture),
					Close = decimal.Parse(values[4], CultureInfo.InvariantCulture),
					AdjClose = decimal.Parse(values[5], CultureInfo.InvariantCulture),
					Volume = long.Parse(values[6], CultureInfo.InvariantCulture),
				};
				historicalPriceData.Add(newPriceData);
			}

			return historicalPriceData;
		}

		private string GetHistoricalDataAsCsv(string yahooStockCode, HistoryType historyType, DateTime? startDate, DateTime? endDate)
		{
			var dateRangeOption = string.Empty;
			var addDateRangeOption = startDate.HasValue && endDate.HasValue;
			if (addDateRangeOption)
			{
				var startDateValue = startDate.Value;
				var endDateValue = endDate.Value;

				dateRangeOption = GetDateRangeOption(startDateValue, endDateValue);
			}

			var historyTypeOption = GetHistoryType(historyType);
			var options = $"{dateRangeOption}{historyTypeOption}";

			var historicalDataCsv = YahooApiRequest(yahooStockCode, options);

			return historicalDataCsv;
		}

		public YahooRealTimeData GetRealTimeData(string yahooStockCode)
		{
			var RealTimeDataCsv = GetRealTimeDataAsCsv(yahooStockCode);


			var values = RealTimeDataCsv.Replace("\"", "").Split(',');


			var realTimeData = new YahooRealTimeData
			{
				Ask = decimal.Parse(values[0], CultureInfo.InvariantCulture),
				Bid = decimal.Parse(values[1], CultureInfo.InvariantCulture),
				Last = decimal.Parse(values[2], CultureInfo.InvariantCulture),
				PreviousClose = decimal.Parse(values[3], CultureInfo.InvariantCulture),
				Open = decimal.Parse(values[4], CultureInfo.InvariantCulture),
				High = decimal.Parse(values[5], CultureInfo.InvariantCulture),
				Low = decimal.Parse(values[6], CultureInfo.InvariantCulture),
				LastTradeTime = DateTime.Parse(values[7], CultureInfo.InvariantCulture),
				Name = values[8],
				Symbol = values[9],
				Volume = long.Parse(values[10], CultureInfo.InvariantCulture),
			};

			return realTimeData;
		}

		private string GetRealTimeDataAsCsv(string yahooStockCode)
		{

			var realTimeDataCsv = YahooRealTimeApiRequest(yahooStockCode);

			return realTimeDataCsv;
		}

		private string YahooApiRequest(string yahooStockCode, string options)
		{
			var baseAddress = new Uri(BaseUrl);

			var requestUrl = $"{BasePath}{yahooStockCode}?{options}&crumb={Crumb}";
			var cookieContainer = new CookieContainer();
			cookieContainer.Add(baseAddress, new Cookie("B", Cookie));

			using (var handler = new HttpClientHandler() { UseCookies = true, CookieContainer = cookieContainer })
			{
				using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
				{
					using (var response = client.GetAsync(requestUrl).Result)
					{
						if (response.IsSuccessStatusCode)
						{
							var result = response.Content.ReadAsStringAsync().Result;
							return result;
						}

						return string.Empty;
					}
				}
			}
		}

		private string YahooRealTimeApiRequest(string yahooStockCode)
		{
			var requestUrl = $"{RealTimeUrl}{yahooStockCode}{RealTimeSuffix}";

			using (var client = new HttpClient())
			{
				using (var response = client.GetAsync(requestUrl).Result)
				{
					var realTimeData = response.Content.ReadAsStringAsync().Result;

					if (response.IsSuccessStatusCode)
					{
						return realTimeData;
					}

					return string.Empty;
				}
			}
		}

		private string GetHistoryType(HistoryType type)
		{
			var optionCode = string.Empty;
			switch (type)
			{
				case HistoryType.DividendHistory:
					optionCode = "1d&events=dividends";
					break;
				case HistoryType.Day:
					optionCode = "1d&events=history";
					break;
				case HistoryType.Week:
					optionCode = "1wk&events=history";
					break;
				case HistoryType.Month:
					optionCode = "1mo&events=history";
					break;
			}

			var option = $"&interval={optionCode}";
			return option;
		}

		private string GetDateRangeOption(DateTimeOffset startDate, DateTimeOffset endDate)
		{
			var start = startDate.ToUnixTimeSeconds();
			var end = endDate.ToUnixTimeSeconds();

			var option = $"period1={start}&period2={end}";
			return option;
		}
	}
}
