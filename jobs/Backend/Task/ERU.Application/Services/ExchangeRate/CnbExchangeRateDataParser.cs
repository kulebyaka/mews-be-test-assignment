﻿using System.Globalization;
using ERU.Application.DTOs;
using ERU.Application.Exceptions;
using ERU.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ERU.Application.Services.ExchangeRate;

public class CnbExchangeRateDataParser : IDataStringParser<IEnumerable<CnbExchangeRateResponse>>
{
	private readonly ConnectorSettings _connectorSettings;

	public CnbExchangeRateDataParser(IOptions<ConnectorSettings> connectorSettingsConfiguration)
	{
		_connectorSettings = connectorSettingsConfiguration.Value;
	}

	public IEnumerable<CnbExchangeRateResponse> Parse(string input)
	{
		string[] lines = (input ?? throw new ArgumentNullException(nameof(input)))
			.Split("\n", StringSplitOptions.RemoveEmptyEntries);
		var foundRates = lines
			.Skip(_connectorSettings.DataSkipLines)
			.Select(ParseExchangeRateLine)
			.Where(line => line != null)
			.ToList();
		return (foundRates.Any() ? foundRates : throw new EmptyParseResultException())!;
	}

	private CnbExchangeRateResponse? ParseExchangeRateLine(string line)
	{
		string[] cells = line.Split(_connectorSettings.DataDelimiter);
		string rawSourceAmount = cells[_connectorSettings.AmountIndex];
		string rawCurrencyCode = cells[_connectorSettings.CodeIndex];
		string rawRate = cells[_connectorSettings.RateIndex];

		if (decimal.TryParse(rawSourceAmount, out decimal sourceAmount)
		    && decimal.TryParse(rawRate, NumberStyles.Currency, CultureInfo.InvariantCulture, out decimal targetAmount))
		{
			return new CnbExchangeRateResponse(sourceAmount, rawCurrencyCode, targetAmount);
		}

		return null;
	}
}