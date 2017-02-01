using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ImportTemperatureMeteoInfo
{
	/// <summary>
	/// Проводит разбор структуры страниц сайта meteoinfo.ru
	/// </summary>
	static class MeteoInfoParser
	{
		public static Dictionary<string, string> ParseOptions(string homePageString, string selectName)
		{
			var result = new Dictionary<string, string>();

			string[] homePageContent = Regex.Split(homePageString, Environment.NewLine);

			bool startFound = false;

			foreach (var str in homePageContent)
			{
				if (!startFound)
				{
					if (ContainsSelector(selectName, str))
					{
						startFound = true;
					}
				}
				else if (ContainsSelectorEnd(str))
				{
					break;
				}
				else
				{
					var region = GetOption(str);

					if (region != null)
					{
						result[region.Name] = region.UrlPart;
					}
				}
			}

			return result;
		}

		public static float ExtractTemperature(string hourContent)
		{
			var strings = Regex.Split(hourContent, Environment.NewLine);

			var pattern = new Regex(@"^\<td class=pogodacell\>\<b\>(?<temperature>[\+\-0-9\.]+)\</b\>\</td\>$");

			foreach (var str in strings)
			{
				var match = pattern.Match(str.Trim());

				if (match.Success)
				{
					string sTemp = match.Groups["temperature"].Value;

					return float.Parse(sTemp, System.Globalization.CultureInfo.InvariantCulture);
				}
			}

			throw new Exception("На странице не найдена температура наружного воздуха.");
		}

		private static Option GetOption(string str)
		{
			var pattern = new Regex(@"^\<option value\=""(?<regionUrlPart>[a-zA-Z0-9\-/]+)""( selected)?>(?<regionName>.+)\</option\>");

			var match = pattern.Match(str);

			if (!match.Success)
			{
				return null;
			}
			else
			{
				return new Option(match.Groups["regionUrlPart"].Value, match.Groups["regionName"].Value);
			}
		}

		private static bool ContainsSelector(string selectorName, string str)
		{
			return str.Contains($"<select name={selectorName}");
		}

		private static bool ContainsSelectorEnd(string str)
		{
			return str.Contains("</select>");
		}
	}
}
