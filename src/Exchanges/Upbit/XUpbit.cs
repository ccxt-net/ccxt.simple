using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace CCXT.Simple.Exchanges.Upbit
{
    public class XUpbit : IExchange
    {
        /*
		 * Upbit Support Markets: KRW, USDT, BTC
		 *
		 * REST API
		 *     https://docs.upbit.com/reference
		 *     https://docs.upbit.com/docs/upbit-quotation-websocket
		 *     https://upbit.com/service_center/wallet_status
		 *
		 * EXCHANGE API
		 *     [주문 요청] 초당 8회, 분당 200회
		 *     [주문 요청 외 API] 초당 30회, 분당 900회
		 *
		 * QUOTATION API
		 *     Websocket 연결 요청 수 제한: 초당 5회, 분당 100회
		 *     REST API 요청 수 제한: 분당 600회, 초당 10회 (종목, 캔들, 체결, 티커, 호가별)
		 */

        public XUpbit(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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
        
        public string ExchangeName { get; set; } = "upbit";

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
                    using HttpResponseMessage _b_response = await _wc.GetAsync("https://api.upbit.com/v1/market/all?isDetails=true");
                    var _jstring = await _b_response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<Market>>(_jstring);

                    _queue_info.symbols.Clear();

                    foreach (var s in _jarray)
                    {
                        var _symbol = s.market;

                        var _pairs = _symbol.Split('-');
                        if (_pairs.Length < 2)
                            continue;

                        if (_pairs[0] == "KRW" || _pairs[0] == "BTC" || _pairs[0] == "USDT")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = _symbol,
                                baseName = _pairs[1],
                                quoteName = _pairs[0]
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2315);
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
        public async ValueTask CheckState(WStates states)
        {
            try
            {
                states.exchange = ExchangeName;

                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _b_response = await _wc.GetAsync("https://ccx.upbit.com/api/v1/status/wallet");
                    var _jstring = await _b_response.Content.ReadAsStringAsync();
                    var _jarray = JArray.Parse(_jstring);

                    foreach (var s in _jarray)
                    {
                        var _currency = s.Value<string>("currency");

                        var _wallet_state = s.Value<string>("wallet_state");
                        var _block_state = s.Value<string>("block_state");
                        var _block_height = s.Value<string>("block_height") != null ? s.Value<long>("block_height") : 0;
                        var _block_updated = s.Value<string>("block_updated_at") != null ? s.Value<DateTime>("block_updated_at") : DateTime.MinValue;
                        var _block_elapsed = s.Value<string>("block_elapsed_minutes") != null ? s.Value<int>("block_elapsed_minutes") : 0;

                        // working, paused, withdraw_only, deposit_only, unsupported
                        var _active = _wallet_state != "unsupported";
                        var _deposit = _wallet_state == "working" || _wallet_state == "deposit_only";
                        var _withdraw = _wallet_state == "working" || _wallet_state == "withdraw_only";

                        var _state = states.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            var _wstate = new WState
                            {
                                currency = _currency,
                                active = _active,

                                deposit = _deposit,
                                withdraw = _withdraw,

                                height = _block_height,
                                updated = _block_updated,
                                elapsed = _block_elapsed,

                                travelRule = true,
                                networks = new List<WNetwork>()
                            };

                            states.states.Add(_wstate);
                        }
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 2316);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2317);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.upbit.com/v1/ticker?markets=" + symbol);
                    var _ticker = await _response.Content.ReadAsStringAsync();
                    var _jstring = _ticker.Substring(1, _ticker.Length - 2);

                    var _jobject = JObject.Parse(_jstring);
                    _result = _jobject.Value<decimal>("trade_price");

                    Debug.Assert(_result != 0.0m);
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2318);
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
                    var _request = String.Join(",", tickers.items.Where(x => x.symbol != "X").Select(x => x.symbol));

                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.upbit.com/v1/ticker?markets=" + _request);
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _j_array = _tstring
                                        .Substring(1, _tstring.Length - 2)
                                        .Replace("},{", "}/{")
                                        .Split('/');

                    var _offset = 0;

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jobject = JObject.Parse(_j_array[_offset++]);

                        var _price = _jobject.Value<decimal>("trade_price");
                        {
                            if (_ticker.symbol == "KRW-BTC")
                                this.mainXchg.OnKrwPriceEvent(_price);

                            if (_ticker.quoteName == "USDT")
                                _ticker.lastPrice = _price * tickers.exchgRate;
                            else if (_ticker.quoteName == "BTC")
                                _ticker.lastPrice = _price * mainXchg.btc_krw_price;
                            else
                                _ticker.lastPrice = _price;
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2319);
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
                    var _request = String.Join(",", tickers.items.Where(x => x.symbol != "X").Select(x => x.symbol));

                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.upbit.com/v1/ticker?markets=" + _request);
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _j_array = _tstring
                                        .Substring(1, _tstring.Length - 2)
                                        .Replace("},{", "}/{")
                                        .Split('/');

                    var _offset = 0;

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jobject = JObject.Parse(_j_array[_offset++]);

                        // UTC 0시 부터 누적 거래액
                        var _volume = _jobject.Value<decimal>("acc_trade_price");
                        {
                            var _prev_volume24h = _ticker.previous24h;
                            var _next_timestamp = _ticker.timestamp + 60 * 1000;

                            if (_ticker.quoteName == "USDT")
                                _volume *= tickers.exchgRate;
                            else if (_ticker.quoteName == "BTC")
                                _volume *= mainXchg.btc_krw_price;

                            _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                            var _curr_timestamp = _jobject.Value<long>("timestamp");
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
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2321);
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
                    var _request = String.Join(",", tickers.items.Where(x => x.symbol != "X").Select(x => x.symbol));

                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.upbit.com/v1/ticker?markets=" + _request);
                    var _tickers = await _response.Content.ReadAsStringAsync();

                    var _data = _tickers
                                    .Substring(1, _tickers.Length - 2)
                                    .Replace("},{", "}/{")
                                    .Split('/');

                    foreach (var d in _data)
                    {
                        var _jobject = JObject.Parse(d);
                        var _coin_name = _jobject.Value<string>("market");

                        var _ticker = tickers.items.Find(x => x.symbol == _coin_name);
                        if (_ticker == null)
                            continue;

                        var _price = _jobject.Value<decimal>("trade_price");
                        {
                            if (_ticker.quoteName == "KRW")
                            {
                                if (_coin_name == "KRW-BTC")
                                    this.mainXchg.OnKrwPriceEvent(_price);

                                _ticker.lastPrice = _price;
                            }
                            else if (_ticker.quoteName == "USDT")
                            {
                                _ticker.lastPrice = _price * tickers.exchgRate;
                            }
                            else if (_ticker.quoteName == "BTC")
                            {
                                _ticker.lastPrice = _price * mainXchg.btc_krw_price;
                            }
                        }

                        // UTC 0시 부터 누적 거래액
                        var _volume = _jobject.Value<decimal>("acc_trade_price");
                        {
                            var _prev_volume24h = _ticker.previous24h;
                            var _next_timestamp = _ticker.timestamp + 60 * 1000;

                            if (_ticker.quoteName == "USDT")
                                _volume *= tickers.exchgRate;
                            else if (_ticker.quoteName == "BTC")
                                _volume *= mainXchg.btc_krw_price;

                            _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                            var _curr_timestamp = _jobject.Value<long>("timestamp");
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
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2322);
            }

            return _result;
        }

        ValueTask<bool> IExchange.GetBookTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }
    }
}