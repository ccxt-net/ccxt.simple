using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace CCXT.Simple.Exchanges.Kucoin
{
    public class XKucoin : IExchange
    {
        /*
		 * Kucoin Support Markets: USDT, BTC
		 *
		 * REST API
		 *     https://docs.kucoin.com/#general
		 *     https://docs.kucoin.com/#symbol-snapshot
		 *
		 */

        public XKucoin(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
        {
            this.mainXchg = mainXchg;

            this.ApiKey = apiKey;
            this.SecretKey = secretKey;
            this.PassPhrase = passPhrase;
        }

        public Exchange mainXchg
        {
            get;
            set;
        }
        
        public string ExchangeName { get; set; } = "kucoin";

        public string ExchangeUrl { get; set; } = "https://api.kucoin.com";

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

        public string SecretKey
        {
            get;
            set;
        }

        public string PassPhrase
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
                var _queue_info = (QueueInfo)null;
                if (!this.mainXchg.exchangeCs.TryGetValue(ExchangeName, out _queue_info))
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v1/symbols");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["data"].ToObject<JArray>();

                    _queue_info.symbols.Clear();

                    foreach (JToken s in _jarray)
                    {
                        var _quote_name = s.Value<string>("quoteCurrency");

                        if (_quote_name == "USDT" || _quote_name == "BTC")
                        {
                            var _symbol = s.Value<string>("symbol");
                            var _base_name = s.Value<string>("baseCurrency");

                            if (_base_name != _symbol.Split('-')[0])
                                _base_name = _symbol.Split('-')[0];

                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = _symbol,
                                compName = _base_name,
                                baseName = _base_name,
                                quoteName = _quote_name,
                                tickSize = s.Value<decimal>("priceIncrement")
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2111);
            }
            finally
            {
                this.Alive = _result;
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
                    __encryptor = new HMACSHA256(Encoding.UTF8.GetBytes(this.SecretKey));

                return __encryptor;
            }
        }

        private void CreateSignature(HttpClient client, string endpoint)
        {
            var _timestamp = CUnixTime.NowMilli.ToString();

            var _sign_data = _timestamp + "GET" + endpoint;
            var _sign_hash = Convert.ToBase64String(Encryptor.ComputeHash(Encoding.UTF8.GetBytes(_sign_data)));
            var _sign_pass = Convert.ToBase64String(Encryptor.ComputeHash(Encoding.UTF8.GetBytes(this.PassPhrase)));

            client.DefaultRequestHeaders.Add("KC-API-SIGN", _sign_hash);
            client.DefaultRequestHeaders.Add("KC-API-TIMESTAMP", _timestamp);
            client.DefaultRequestHeaders.Add("KC-API-KEY", this.ApiKey);
            client.DefaultRequestHeaders.Add("KC-API-PASSPHRASE", _sign_pass);
            client.DefaultRequestHeaders.Add("KC-API-KEY-VERSION", "2");
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask CheckState(WStates states)
        {
            try
            {
                states.exchange = ExchangeName;

                using (var _wc = new HttpClient())
                {
                    var _end_point = "/api/v1/currencies";

                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}{_end_point}");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<CoinInfor>(_jstring);

                    foreach (var c in _jarray.data)
                    {
                        var _state = states.states.SingleOrDefault(x => x.currency == c.currency);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = c.currency,
                                active = true,
                                deposit = c.isDepositEnabled,
                                withdraw = c.isWithdrawEnabled,
                                networks = new List<WNetwork>()
                            };

                            states.states.Add(_state);
                        }
                        else
                        {
                            _state.deposit = c.isDepositEnabled;
                            _state.withdraw = c.isWithdrawEnabled;
                        }

                        var _name = c.currency + "-" + c.name;

                        var _network = _state.networks.SingleOrDefault(x => x.name == _name);
                        if (_network == null)
                        {
                            _state.networks.Add(new WNetwork
                            {
                                name = _name,
                                network = c.name,
                                protocol = "",

                                deposit = c.isDepositEnabled,
                                withdraw = c.isWithdrawEnabled,

                                minWithdrawal = c.withdrawalMinSize,
                                withdrawFee = c.withdrawalMinFee,

                                minConfirm = c.confirms
                            });
                        }
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 2112);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2113);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v1/market/orderbook/level1?symbol=" + symbol);
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"];
                    _result = _jobject.Value<decimal>("price");
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2114);
            }

            return _result;
        }

        /// <summary>
        /// Get Upbit Tickers
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v1/market/allTickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"]["ticker"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("last");
                            {
                                if (_ticker.quoteName == "USDT")
                                {
                                    _ticker.lastPrice = _last_price * tickers.exchgRate;
                                }
                                else if (_ticker.quoteName == "BTC")
                                {
                                    _ticker.lastPrice = _last_price * mainXchg.btc_krw_price;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2115);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2116);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetBookTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v1/market/allTickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"]["ticker"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("last");
                            {
                                var _ask_price = _jitem.Value<decimal>("sell");
                                var _bid_price = _jitem.Value<decimal>("buy");

                                if (_ticker.quoteName == "USDT")
                                {
                                    _ticker.lastPrice = _last_price * tickers.exchgRate;

                                    _ticker.askPrice = _ask_price * tickers.exchgRate;
                                    _ticker.bidPrice = _bid_price * tickers.exchgRate;
                                }
                                else if (_ticker.quoteName == "BTC")
                                {
                                    _ticker.lastPrice = _last_price * mainXchg.btc_krw_price;

                                    _ticker.askPrice = _ask_price * mainXchg.btc_krw_price;
                                    _ticker.bidPrice = _bid_price * mainXchg.btc_krw_price;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2117);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2118);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetVolumes(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v1/market/allTickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"]["ticker"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _volume = _jitem.Value<decimal>("volValue");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT")
                                    _volume *= tickers.exchgRate;
                                else if (_ticker.quoteName == "BTC")
                                    _volume *= mainXchg.btc_krw_price;

                                _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                                var _curr_timestamp = CUnixTime.NowMilli;
                                if (_curr_timestamp > _next_timestamp)
                                {
                                    _ticker.volume1m = Math.Floor((_prev_volume24h > 0 ? _volume - _prev_volume24h : 0) / mainXchg.Volume1mBase);

                                    _ticker.timestamp = _curr_timestamp;
                                    _ticker.previous24h = _volume;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2119);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2120);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetMarkets(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v1/market/allTickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"]["ticker"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("last");
                            {
                                var _ask_price = _jitem.Value<decimal>("sell");
                                var _bid_price = _jitem.Value<decimal>("buy");

                                if (_ticker.quoteName == "USDT")
                                {
                                    _ticker.lastPrice = _last_price * tickers.exchgRate;

                                    _ticker.askPrice = _ask_price * tickers.exchgRate;
                                    _ticker.bidPrice = _bid_price * tickers.exchgRate;
                                }
                                else if (_ticker.quoteName == "BTC")
                                {
                                    _ticker.lastPrice = _last_price * mainXchg.btc_krw_price;

                                    _ticker.askPrice = _ask_price * mainXchg.btc_krw_price;
                                    _ticker.bidPrice = _bid_price * mainXchg.btc_krw_price;
                                }
                            }

                            var _volume = _jitem.Value<decimal>("volValue");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT")
                                    _volume *= tickers.exchgRate;
                                else if (_ticker.quoteName == "BTC")
                                    _volume *= mainXchg.btc_krw_price;

                                _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                                var _curr_timestamp = CUnixTime.NowMilli;
                                if (_curr_timestamp > _next_timestamp)
                                {
                                    _ticker.volume1m = Math.Floor((_prev_volume24h > 0 ? _volume - _prev_volume24h : 0) / mainXchg.Volume1mBase);

                                    _ticker.timestamp = _curr_timestamp;
                                    _ticker.previous24h = _volume;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2121);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2122);
            }

            return _result;
        }
    }
}