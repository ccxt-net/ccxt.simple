using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CCXT.Simple.Exchanges.Crypto
{
    public class XCrypto : IExchange
    {
        /*
		 * Crypto Support Markets: USDT, USDC, BTC
		 *
		 * REST API
		 *     https://docs.crypto.com/#general
		 *     https://docs.crypto.com/#symbol-snapshot
		 *
		 */

        public XCrypto(Exchange mainXchg)
        {
            this.mainXchg = mainXchg;
        }

        private Exchange mainXchg
        {
            get;
            set;
        }

        
        public string ExchangeName { get; set; } = "crypto";

        public bool Alive
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.crypto.com/v2/public/get-instruments");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<Exchanges.Crypto.Market>(_jstring);

                    _queue_info.symbols.Clear();

                    foreach (var s in _jarray.result.instruments)
                    {
                        s.quote_currency = s.quote_currency.Split('_')[0].Split(' ')[0];

                        if (s.quote_currency == "USDT" || s.quote_currency == "USD" || s.quote_currency == "BTC")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.instrument_name,
                                name = s.instrument_name,
                                tickSize = s.min_quantity,
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
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1711);
            }
            finally
            {
                this.Alive = _result;
            }

            return _result;
        }

        public async ValueTask<bool> GetMarkets(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.crypto.com/v2/public/get-ticker");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["result"]["data"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["i"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("a");
                            {
                                var _ask_price = _jitem.Value<decimal>("k");
                                var _bid_price = _jitem.Value<decimal>("b");

                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USD")
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

                            var _volume = _jitem.Value<decimal>("vv");      // The total 24h traded volume value (in USD)
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                _volume *= tickers.exchgRate;
                                _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                                var _curr_timestamp = _jitem.Value<long>("t");
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1712);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1713);
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