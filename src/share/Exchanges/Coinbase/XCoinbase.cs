using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CCXT.Simple.Exchanges.Coinbase
{
    public class XCoinbase : IExchange
    {
        /*
		 * CoinbasePro Support Markets: BTC,USDC,USDT,USD
		 *
		 * Rate Limit
		 *     https://docs.cloud.coinbase.com/exchange/docs/rate-limits
		 *
		 *     Public endpoints
		 *         We throttle public endpoints by IP: 10 requests per second, up to 15 requests per second in bursts.
		 *
		 *     Private endpoints
		 *         We throttle private endpoints by profile ID: 15 requests per second, up to 30 requests per second in bursts.
		 */

        public XCoinbase(Exchange mainXchg, string apiKey, string secretKey, string passPharase)
        {
            this.mainXchg = mainXchg;

            this.ApiKey = apiKey;
            this.SecretKey = secretKey;
            this.Passphrase = passPharase;
        }

        private Exchange mainXchg
        {
            get;
            set;
        }
        
        public string ExchangeName { get; set; } = "coinbase";

        public bool Alive
        {
            get;
            set;
        }

        public string ApiKey
        {
            get;
            set;
        }

        public string Passphrase
        {
            get;
            set;
        }

        public string SecretKey
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> VerifyCoinNames()
        {
            var _result = false;

            try
            {
                if (!this.mainXchg.exchangeCs.TryGetValue(ExchangeName, out var _queue_info))
                {
                    _queue_info = new QueueInfo
                    {                        
                        name = ExchangeName,
                        symbols = new List<QueueSymbol>()
                    };

                    this.mainXchg.exchangeCs.TryAdd(ExchangeName, _queue_info);
                }

                using (var _wc = new HttpClient())
                {
                    _wc.DefaultRequestHeaders.Add("User-Agent", mainXchg.UserAgent);

                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.pro.coinbase.com/products");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<Exchanges.Coinbase.Market>>(_jstring);

                    _queue_info.symbols.Clear();

                    foreach (var s in _jarray)
                    {
                        if (s.quote_currency == "USDT" || s.quote_currency == "USD" || s.quote_currency == "BTC")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.id,
                                name = s.display_name,
                                baseName = s.base_currency,
                                quoteName = s.quote_currency
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1512);
            }
            finally
            {
                this.Alive = _result;
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> CheckState(WStates states)
        {
            var _result = false;

            try
            {
                states.exchange = ExchangeName;

                using (var _wc = new HttpClient())
                {
                    this.CreateSignature(_wc);

                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.exchange.coinbase.com/coinbase-accounts");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JArray.Parse(_jstring);

                    foreach (JToken s in _jarray)
                    {
                        var _currency = s.Value<string>("currency");

                        var _state = states.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            states.states.Add(new WState
                            {
                                currency = _currency,
                                active = s.Value<bool>("active"),
                                deposit = true,
                                withdraw = true
                            });
                        }
                        else
                        {
                            _state.active = s.Value<bool>("active");
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1513);
            }

            return _result;
        }

        private HMACSHA256 __encryptor = null;

        /// <summary>
        ///
        /// </summary>
        public HMACSHA256 Encryptor
        {
            get
            {
                if (__encryptor == null)
                    __encryptor = new HMACSHA256(Convert.FromBase64String(this.SecretKey));

                return __encryptor;
            }
        }

        private void CreateSignature(HttpClient client)
        {
            var _timestamp = CUnixTime.Now;

            var _post_data = $"{_timestamp}GET/coinbase-accounts";
            var _signature = Convert.ToBase64String(Encryptor.ComputeHash(Encoding.UTF8.GetBytes(_post_data)));

            client.DefaultRequestHeaders.Add("USER-AGENT", mainXchg.UserAgent);
            client.DefaultRequestHeaders.Add("CB-ACCESS-KEY", this.ApiKey);
            client.DefaultRequestHeaders.Add("CB-ACCESS-SIGN", _signature);
            client.DefaultRequestHeaders.Add("CB-ACCESS-TIMESTAMP", _timestamp.ToString());
            client.DefaultRequestHeaders.Add("CB-ACCESS-PASSPHRASE", this.Passphrase);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="_ticker"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetMarket(Ticker _ticker, decimal exchg_rate)
        {
            var _result = false;

            try
            {
                using (var _request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri("https://api.pro.coinbase.com"), $"/products/{_ticker.symbol}/ticker")))
                {
                    _request.Headers.Add("User-Agent", mainXchg.UserAgent);

                    using var _client = new HttpClient();

                    var _response = await _client.SendAsync(_request);
                    if (_response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var _tstring = await _response.Content.ReadAsStringAsync();
                        var _jobject = JObject.Parse(_tstring);

                        var _price = _jobject.Value<decimal>("price");
                        {
                            if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USD")
                            {
                                _ticker.lastPrice = _price * exchg_rate;

                                _ticker.askPrice = _price * exchg_rate;
                                _ticker.bidPrice = _price * exchg_rate;
                            }
                            else if (_ticker.quoteName == "BTC")
                            {
                                _ticker.lastPrice = _price * mainXchg.btc_krw_price;

                                _ticker.askPrice = _price * mainXchg.btc_krw_price;
                                _ticker.bidPrice = _price * mainXchg.btc_krw_price;
                            }
                        }

                        var _volume = _jobject.Value<decimal>("volume");
                        {
                            var _prev_volume24h = _ticker.previous24h;
                            var _next_timestamp = _ticker.timestamp + 60 * 1000;

                            if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USD")
                                _volume *= _price * exchg_rate;
                            else if (_ticker.quoteName == "BTC")
                                _volume *= _price * mainXchg.btc_krw_price;

                            _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                            var _curr_timestamp = CUnixTime.ConvertToUnixTimeMilli(_jobject.Value<DateTime>("time"));
                            if (_curr_timestamp > _next_timestamp)
                            {
                                _ticker.volume1m = Math.Floor((_prev_volume24h > 0 ? _volume - _prev_volume24h : 0) / mainXchg.Volume1mBase);

                                _ticker.timestamp = _curr_timestamp;
                                _ticker.previous24h = _volume;
                            }
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1514);
            }

            return _result;
        }

        ValueTask IExchange.CheckState(WStates states)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetBookTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetMarkets(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<decimal> IExchange.GetPrice(string symbol)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetVolumes(Tickers tickers)
        {
            throw new NotImplementedException();
        }
    }
}