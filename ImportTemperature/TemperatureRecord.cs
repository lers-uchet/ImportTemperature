﻿using System;

namespace ImportTemperature;

/// <summary>
/// Запись со среднесуточной температурой.
/// </summary>
public class TemperatureRecord
{
	/// <summary>
	/// Дата записи.
	/// </summary>
	public DateTime Date { get; set; }

	/// <summary>
	/// Температура.
	/// </summary>
	public float Temperature { get; set; }
};
