using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

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
        public string ExchangeWwUrl { get; set; } = "https://www.kucoin.com";
        
        public bool Alive { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public string PassPhrase { get; set; }
        

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> VerifySymbols()
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v2/symbols");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<CoinInfor>(_jstring);

                    var _queue_info = this.mainXchg.GetXInfors(ExchangeName);

                    foreach (var s in _jarray.data)
                    {
                        var _quote_name = s.quoteCurrency;

                        if (_quote_name == "USDT" || _quote_name == "BTC")
                        {
                            var _symbol = s.symbol;
                            var _base_name = s.baseCurrency;

                            if (_base_name != _symbol.Split('-')[0])
                                _base_name = _symbol.Split('-')[0];

                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = _symbol,
                                compName = _base_name,
                                baseName = _base_name,
                                quoteName = _quote_name,
                                tickSize = s.priceIncrement
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4001);
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

        public void CreateSignature(HttpClient client, string endpoint)
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
        public async ValueTask<bool> VerifyStates(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    var _end_point = "/_pxapi/pool-currency/currencies";

                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeWwUrl}{_end_point}");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<CoinState>(_jstring);

                    foreach (var c in _jarray.data)
                    {
                        var _state = tickers.states.SingleOrDefault(x => x.currency == c.currency);
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

                            tickers.states.Add(_state);
                        }
                        else
                        {
                            _state.deposit = c.isDepositEnabled;
                            _state.withdraw = c.isWithdrawEnabled;
                        }

                        var _t_items = tickers.items.Where(x => x.compName == _state.currency);
                        if (_t_items != null)
                        {
                            foreach (var t in _t_items)
                            {
                                t.active = _state.active;
                                t.deposit = _state.deposit;
                                t.withdraw = _state.withdraw;
                            }
                        }

                        var _name = c.currency + "-" + c.chainFullName;

                        var _network = _state.networks.SingleOrDefault(x => x.name == _name);
                        if (_network == null)
                        {
                            _state.networks.Add(new WNetwork
                            {
                                name = _name,
                                network = c.chainFullName,
                                chain = c.chainName,

                                deposit = c.isDepositEnabled,
                                withdraw = c.isWithdrawEnabled,

                                minWithdrawal = c.withdrawMinSize,
                                withdrawFee = c.withdrawMinFee,

                                sellFee = c.makerFeeCoefficient,
                                buyFee = c.takerFeeCoefficient,
                                
                                minConfirm = c.confirmationCount
                            });
                        }
                    }

                    _result = true;
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 4002);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4003);
            }

            return _result;
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
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4004);
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
                                    _ticker.lastPrice = _last_price * mainXchg.fiat_btc_price;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 4005);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4006);
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
                                    _ticker.lastPrice = _last_price * mainXchg.fiat_btc_price;

                                    _ticker.askPrice = _ask_price * mainXchg.fiat_btc_price;
                                    _ticker.bidPrice = _bid_price * mainXchg.fiat_btc_price;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 4007);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4008);
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
                                    _volume *= mainXchg.fiat_btc_price;

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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 4009);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4010);
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
                                    _ticker.lastPrice = _last_price * mainXchg.fiat_btc_price;

                                    _ticker.askPrice = _ask_price * mainXchg.fiat_btc_price;
                                    _ticker.bidPrice = _bid_price * mainXchg.fiat_btc_price;
                                }
                            }

                            var _volume = _jitem.Value<decimal>("volValue");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT")
                                    _volume *= tickers.exchgRate;
                                else if (_ticker.quoteName == "BTC")
                                    _volume *= mainXchg.fiat_btc_price;

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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 4011);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4012);
            }

            return _result;
        }
    }
}