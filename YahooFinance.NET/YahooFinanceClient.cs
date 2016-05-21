using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;

namespace YahooFinance.NET
{
	// This project can output the Class library as a NuGet Package.
	// To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
	public class YahooFinanceClient
	{
		private const int MinimumDateRangeDays = -30;
		private readonly CultureInfo _usCulture = new CultureInfo("en-US");

		private const string BaseUrl = "http://ichart.finance.yahoo.com/table.csv?s=";

		private enum HistoryType
		{
			DividendHistory = 1,
			Day,
			Week,
			Month,
		}

		//date range is not supported for monthly data
		public List<YahooHistoricalPriceData> GetMonthlyHistoricalPriceData(string yahooStockCode)
		{
			return GetHistoricalPriceData(yahooStockCode, null, null, HistoryType.Month);
		}

		//date range is not supported for weekly data
		public List<YahooHistoricalPriceData> GetWeeklyHistoricalPriceData(string yahooStockCode)
		{
			return GetHistoricalPriceData(yahooStockCode, null, null, HistoryType.Week);
		}

		public List<YahooHistoricalPriceData> GetDailyHistoricalPriceData(string yahooStockCode)
		{
			return GetHistoricalPriceData(yahooStockCode, null, null, HistoryType.Day);
		}

		public List<YahooHistoricalPriceData> GetDailyHistoricalPriceData(string yahooStockCode, DateTime startDate)
		{
			return GetHistoricalPriceData(yahooStockCode, startDate, DateTime.Today, HistoryType.Day);
		}

		public List<YahooHistoricalPriceData> GetDailyHistoricalPriceData(string yahooStockCode, DateTime startDate, DateTime endDate)
		{
			return GetHistoricalPriceData(yahooStockCode, startDate, endDate, HistoryType.Day);
		}

		private List<YahooHistoricalPriceData> GetHistoricalPriceData(string yahooStockCode, DateTime? startDate, DateTime? endDate, HistoryType historyType)
		{
			var dateRangeOption = string.Empty;
			var addDateRangeOption = historyType == HistoryType.Day && startDate.HasValue && endDate.HasValue;
			if (addDateRangeOption)
			{
				var startDateValue = startDate.Value;
				var endDateValue = endDate.Value;

				//date range must be at least 30 days or the API will return a 404 error
				var dateRangeIsSmallerThenMinimum = startDateValue > endDateValue.AddDays(MinimumDateRangeDays);
				if (dateRangeIsSmallerThenMinimum)
				{
					startDateValue = endDateValue.AddDays(MinimumDateRangeDays);
				}

				dateRangeOption = GetDateRangeOption(startDateValue, endDateValue);
			}

			var historyTypeOption = GetHistoryType(historyType);
			var options = $"{dateRangeOption}{historyTypeOption}";
			var historicalDataCsv = YahooApiRequest(yahooStockCode, options);

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
					Date = DateTime.Parse(values[0], _usCulture),
					Open = decimal.Parse(values[1], _usCulture),
					High = decimal.Parse(values[2], _usCulture),
					Low = decimal.Parse(values[3], _usCulture),
					Close = decimal.Parse(values[4], _usCulture),
					Volume = int.Parse(values[5], _usCulture),
					AdjClose = decimal.Parse(values[6], _usCulture)
				};
				historicalPriceData.Add(newPriceData);
			}

			return historicalPriceData;
		}

		//date range is not supported for dividend data
		public List<YahooHistoricalDividendData> GetHistoricalDividendData(string yahooStockCode)
		{
			var dividendHistoryOption = GetHistoryType(HistoryType.DividendHistory);
			var dividendHistoryCsv = YahooApiRequest(yahooStockCode, dividendHistoryOption);

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
					Date = DateTime.Parse(values[0]),
					Dividend = decimal.Parse(values[1]),
				};
				historicalDevidendData.Add(newDividendData);
			}

			return historicalDevidendData;
		}

		private string YahooApiRequest(string yahooStockCode, string options)
		{
			var requestUrl = $"{BaseUrl}{yahooStockCode}{options}";

			using (var client = new HttpClient())
			{
				using (var response = client.GetAsync(requestUrl).Result)
				{
					var historicalData = response.Content.ReadAsStringAsync().Result;

					return historicalData;
				}
			}
		}

		private string GetHistoryType(HistoryType type)
		{
			var optionCode = string.Empty;
			switch (type)
			{
				case HistoryType.DividendHistory:
					optionCode = "v";
					break;
				case HistoryType.Day:
					optionCode = "d";
					break;
				case HistoryType.Week:
					optionCode = "w";
					break;
				case HistoryType.Month:
					optionCode = "m";
					break;
			}

			var option = $"&g={optionCode}";
			return option;
		}

		private string GetDateRangeOption(DateTime startDate, DateTime endDate)
		{
			var start = $"{GetStartDate(startDate)}";
			var end = $"{GetEndDate(endDate)}";

			var option = $"{start}{end}";
			return option;
		}

		private string GetStartDate(DateTime date)
		{
			var month = $"&a={date.Month}";
			var day = $"&b={date.Day}";
			var year = $"&c={date.Year}";

			var option = $"{month}{day}{year}";
			return option;
		}

		private string GetEndDate(DateTime date)
		{
			var month = $"&d={date.Month}";
			var day = $"&e={date.Day}";
			var year = $"&f={date.Year}";

			var option = $"{month}{day}{year}";
			return option;
		}

		public string GetYahooStockCode(string exchange, string code)
		{
			var exchangeHelper = new YahooExchangeHelper();
			return exchangeHelper.GetYahooStockCode(exchange, code);
		}
	}
}
