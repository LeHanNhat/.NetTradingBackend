using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MainWebAPI.Controllers
{
    [Route("api/")]
    [ApiController]
    
    public class MarketDataController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MarketDataController> _logger;
        private readonly IDistributedCache _cache;

        public MarketDataController(IHttpClientFactory httpClientFactory, ILogger<MarketDataController> logger, IDistributedCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cache = cache;

        }

        [HttpPost]
        [Route("marketData")]
        public async Task<IActionResult> Get([FromBody] ApiRequest req)
        {
            var client = _httpClientFactory.CreateClient();
            string cacheKey = $"marketData_{req.Symbol}_{req.Interval}";
            string formattedData = await _cache.GetStringAsync(cacheKey);
            var stopwatch = new Stopwatch();

            if (string.IsNullOrEmpty(formattedData))
            {
                _logger.LogInformation("Cache miss for key: {CacheKey}", cacheKey);
                stopwatch.Start();
                var externalApiUrl = $"https://api.binance.com/api/v3/klines?symbol={req.Symbol}&interval={req.Interval}&limit=500";

                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(externalApiUrl);
                }
                catch (HttpRequestException e)
                {
                    _logger.LogError(e, "Request error");
                    return StatusCode(500, $"Request error: {e.Message}");
                }
                stopwatch.Stop();
                _logger.LogInformation("Time taken to fetch data from external API: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonData);
                    var rawValuesList = new List<object>();

                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        // Collect only the values you need
                        var rawValues = new List<object>
                {
                    element[0].GetInt64(),      // OpenTime
                    element[1].GetString(),    // Open
                    element[2].GetString(),    // High
                    element[3].GetString(),    // Low
                    element[4].GetString(),    // Close
                    element[5].GetString(),    // Volume
                    element[6].GetInt64(),      // CloseTime
                    element[7].GetString(),    // QuoteAssetVolume
                    element[8].GetInt32(),      // NumberOfTrades
                    element[9].GetString(),    // TakerBuyBaseAssetVolume
                    element[10].GetString()    // TakerBuyQuoteAssetVolume
                };
                        rawValuesList.Add(rawValues);
                    }

                    // Serialize the raw values list
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    formattedData = JsonSerializer.Serialize(rawValuesList, options);

                    // Save data in cache
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    };
                    await _cache.SetStringAsync(cacheKey, formattedData, cacheOptions);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, response.ReasonPhrase);
                }
            }
            else
            {
                stopwatch.Start();
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
                stopwatch.Stop();
                _logger.LogInformation("Time taken to fetch data from cache: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
            }

            return Ok(formattedData);
        }

        [HttpGet("symbols")]
        public async Task<IActionResult> GetExchangeInfo()
        {
            var client = _httpClientFactory.CreateClient();
            var cacheKey = "exchangeInfo";
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            var cachedData = await _cache.GetStringAsync(cacheKey);
            stopwatch.Stop();

            if (string.IsNullOrEmpty(cachedData))
            {
                _logger.LogInformation("Cache miss for key: {CacheKey}", cacheKey);
                _logger.LogInformation("Time taken to fetch data from cache: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                var externalApiUrl = "https://api.binance.com/api/v3/exchangeInfo";
                HttpResponseMessage response;
                try
                {
                    stopwatch.Restart();
                    response = await client.GetAsync(externalApiUrl);
                    stopwatch.Stop();
                }
                catch (HttpRequestException e)
                {
                    _logger.LogError(e, "Request error");
                    return StatusCode(500, $"Request error: {e.Message}");
                }

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    var exchangeInfo = JsonSerializer.Deserialize<ExchangeInfo>(jsonData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    });

                    // Cache the data
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30), // Set as needed
                        SlidingExpiration = TimeSpan.FromMinutes(10)
                    };
                    await _cache.SetStringAsync(cacheKey, jsonData, cacheOptions);

                    _logger.LogInformation("Time taken to fetch data from external API: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                    return Ok(exchangeInfo.Symbols);
                }

                return StatusCode((int)response.StatusCode, response.ReasonPhrase);
            }
            else
            {
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
                _logger.LogInformation("Time taken to fetch data from cache: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                var exchangeInfo = JsonSerializer.Deserialize<ExchangeInfo>(cachedData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                return Ok(exchangeInfo.Symbols);
            }
        }

        [HttpGet("intervals")]
        public async Task<IActionResult> GetIntervals()
        {
            var cacheKey = "intervals";
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            var cachedIntervals = await _cache.GetStringAsync(cacheKey);
            stopwatch.Stop();

            if (string.IsNullOrEmpty(cachedIntervals))
            {
                _logger.LogInformation("Cache miss for key: {CacheKey}", cacheKey);
                _logger.LogInformation("Time taken to check cache: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                var intervals = Intervals.intervals;

                // Cache the intervals
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1), // Set as needed
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                };
                stopwatch.Restart();
                var intervalsJson = JsonSerializer.Serialize(intervals);
                await _cache.SetStringAsync(cacheKey, intervalsJson, cacheOptions);
                stopwatch.Stop();

                _logger.LogInformation("Time taken to cache intervals: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                return Ok(intervals);
            }
            else
            {
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
                _logger.LogInformation("Time taken to fetch data from cache: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                var intervals = JsonSerializer.Deserialize<List<string>>(cachedIntervals);

                return Ok(intervals);
            }
        }

        public static class Intervals
        {
            public static readonly List<string> intervals = new List<string>
                {
                    "1s","1m", "3m", "5m", "15m", "30m",
                    "1H", "2H", "4H", "6H", "8H", "12H",
                    "1D", "3D", "1W", "1M"
                };
        }
        public class ExchangeInfo
        {

            public List<SymbolInfo> Symbols { get; set; }
        }
        public class SymbolInfo
        {

            public string Symbol { get; set; }


        }
        public class ApiRequest
        {
            public string Symbol { get; set; }
            public string Interval { get; set; }

        }

        //public class CandlestickData
        //{
        //    public long OpenTime { get; set; }
        //    public string Open { get; set; }
        //    public string High { get; set; }
        //    public string Low { get; set; }
        //    public string Close { get; set; }
        //    public string Volume { get; set; }
        //    public long CloseTime { get; set; }
        //    public string QuoteAssetVolume { get; set; }
        //    public int NumberOfTrades { get; set; }
        //    public string TakerBuyBaseAssetVolume { get; set; }
        //    public string TakerBuyQuoteAssetVolume { get; set; }
        //}
    }
}
