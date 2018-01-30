using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ImportTemperatureMeteoInfo
{
	/// <summary>
	/// Проводит разбор структуры страниц сайта meteoinfo.ru
	/// </summary>
	internal static class MeteoInfoParser
	{
		public static Dictionary<string, string> ParseOptions(string homePageString, string selectName)
		{
			var result = new Dictionary<string, string>();

			string[] homePageContent = Regex.Split(homePageString, Environment.NewLine);

			foreach (var str in homePageContent)
			{
				if (ContainsSelector(selectName, str))
				{
					var option = GetOptions(str);

					if (option != null)
					{
						result = option.ToDictionary(x => x.Name, y => y.UrlPart);
					}
				}
			}

			return result;
		}

		public static float? ExtractTemperature(string hourContent)
		{
			// Сейчас сайт возвращает информацию по температурам в видела html кода таблицы, среди неё нам необходим найти данную строку и выделить
			// из неё температуру.
			//<td width=\"50%\"  style=\"border-bottom: 1px solid #D3D3D3;\"  align=\"right\">Температура воздуха, &deg;C</td><td width=\"50%\"  style=\"border-bottom: 1px solid #D3D3D3;\"  align=\"center\">-10.6</td>

			var pattern = new Regex(@"\<td width=\\""\d+%\\""\s+style=\\"".{35}\s\salign=.{10}Температура\sвоздуха.{93}>(?<temperature>\D?\d+\.?\d?)\<\/td>");

			float? result = null;

			var match = pattern.Match(hourContent);

			if (match.Success)
			{
				string sTemp = match.Groups["temperature"].Value;

				result = float.Parse(sTemp, System.Globalization.CultureInfo.InvariantCulture);
			}

			return result;
		}

		private static IEnumerable<Option> GetOptions(string str)
		{
			// Список представляет собой выпадающий список, содержащий подобные записи. необходимо выделить Id и название.
			// <option value="1987">Абакан, Россия, Хакасия республика</option>
			var pattern = new Regex(@"\<option value\=""(?<regionUrlPart>[a-zA-Z0-9\-/]+)"">(?<regionName>\D+)\</option\>");

			var matches = pattern.Matches(str);

			if (matches.Count == 0)
			{
				yield return null;
			}

			foreach (Match match in matches)
			{
				yield return new Option(match.Groups["regionUrlPart"].Value, match.Groups["regionName"].Value);
			}
		}

		private static bool ContainsSelector(string selectorName, string str)
		{
			return str.Contains($"<select name=\"{selectorName}\"");
		}

		private static bool ContainsSelectorEnd(string str)
		{
			return str.Contains("</select>");
		}

		public static Dictionary<DateTime, string> ParseTimeStamps(string raw)
		{
			var result = new Dictionary<DateTime, string>();

			// Метки времени с идентификаторами приходят в виде строки содержащей подобные записи:
			// <option value="1517263200">29-01-2018 22:00</option>
			// Нам необходимо выделить идентификатор, для дальнейшего выполнения POST запросов и метку времени.
			var pattern = new Regex(@"\<option value\=\\""(?<dataId>[0-9]+)\\"">(?<dateTime>\d\d-\d\d-\d{4}\s\d\d:\d\d)\</option\>");

			var matches = pattern.Matches(raw);

			foreach (Match match in matches)
			{
				var dateTime = DateTime.ParseExact(match.Groups["dateTime"].Value, "dd-MM-yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

				result[dateTime] = match.Groups["dataId"].Value;
			}

			return result;
		}
	}
}