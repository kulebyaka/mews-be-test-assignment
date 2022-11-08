﻿using ERU.Application.DTOs;
using ERU.Application.Interfaces;
using ERU.Domain;
using Microsoft.Extensions.Options;

namespace ERU.Application.Services.ExchangeRate;

public interface ICnbDataExtractor
{
	Task<IEnumerable<CnbExchangeRateResponse>> CnbExchangeRateResults(IEnumerable<string> currencyCodes, string cacheKey, CancellationToken token);
}

public class CnbDataExtractor : ICnbDataExtractor
{
	private readonly IHttpClient _client;
	private readonly IDataStringParser<IEnumerable<CnbExchangeRateResponse>> _parser;
	private readonly ConnectorSettings _connectorSettings;
	private readonly MemoryCacheHelper _memoryCacheHelper;
	public CnbDataExtractor(IHttpClient client, IDataStringParser<IEnumerable<CnbExchangeRateResponse>> parser, IOptions<ConnectorSettings> connectorSettingsConfiguration, MemoryCacheHelper memoryCacheHelper)
	{
		_client = client;
		_parser = parser;
		_connectorSettings = connectorSettingsConfiguration.Value;
		_memoryCacheHelper = memoryCacheHelper;
	}

	public async Task<IEnumerable<CnbExchangeRateResponse>> CnbExchangeRateResults(IEnumerable<string> currencyCodes, string cacheKey, CancellationToken token)
	{
		var allRates = _memoryCacheHelper.GetFromCache<IEnumerable<CnbExchangeRateResponse>>(cacheKey);
		if (allRates == null)
		{
			allRates = await SearchUntilFindAllCodes(_connectorSettings.FileUri.ToList(), currencyCodes, token);
			_memoryCacheHelper.InsertToCache(cacheKey, allRates);
		}
		return allRates;
	}

	private async Task<IEnumerable<CnbExchangeRateResponse>> SearchUntilFindAllCodes(List<string> urls, IEnumerable<string> currencyCodes, CancellationToken token)
	{
		var tasks = new HashSet<Task<IEnumerable<CnbExchangeRateResponse>>>();
		var results = new List<CnbExchangeRateResponse>();
		var cts = CancellationTokenSource.CreateLinkedTokenSource(token); 
		foreach (string url in urls)
		{
			tasks.Add( GetDataAndParse(url, cts.Token));
		}
		
		while (tasks.Count > 0)
		{
			var task = await Task.WhenAny(tasks);
			var result = (await task).ToList();
			if (result.Any() && currencyCodes.All(c=> result.Select(cur=>cur.Code).Contains(c)))
			{
				cts.Cancel();
				results.AddRange(result);
				return results;
			}
			results.AddRange(result);
			tasks.Remove(task);
		}
		return results;
	}

	private async Task<IEnumerable<CnbExchangeRateResponse>> GetDataAndParse(string url, CancellationToken token)
	{
		string rates = await _client.GetStringAsync(url, token);
		// TODO: dictionary of currencies and their codes? 
		return _parser.Parse(rates);
	}
}