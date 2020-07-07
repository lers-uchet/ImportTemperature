using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ImportTemperatureMeteoInfo.Importers
{
	/// <summary>
	/// Проводит разбор структуры страниц сайта meteoinfo.ru
	/// </summary>
	internal static class MeteoInfoParser
	{
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

		public static IEnumerable<Option> GetOptions(string str)
		{
			// Список представляет собой выпадающий список, содержащий подобные записи. необходимо выделить Id и название.
			// <option value="1987">Абакан, Россия, Хакасия республика</option>
			var pattern = new Regex(@"<option  value\=\\""(?<regionUrlPart>\d+)\\"">(?<regionName>\D+)\<\/option\>");

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

		public static Dictionary<DateTime, string> ParseTimeStamps(string raw)
		{
			var result = new Dictionary<DateTime, string>();

			// Метки времени с идентификаторами приходят в виде строки содержащей подобные записи:
			// <option value="1517263200">2019-05-21 22:00</option>
			// Нам необходимо выделить идентификатор, для дальнейшего выполнения POST запросов и метку времени.
			
			var pattern = new Regex(@"\<option value\=\\""(?<dataId>[0-9]+)\\"">(?<dateTime>\d{4}-\d{2}-\d{2}\s\d\d:\d\d)\</option\>");

			var matches = pattern.Matches(raw);

			foreach (Match match in matches)
			{
				var dateTime = DateTime.ParseExact(match.Groups["dateTime"].Value, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

				result[dateTime] = match.Groups["dataId"].Value;
			}

			return result;
		}
	}
}