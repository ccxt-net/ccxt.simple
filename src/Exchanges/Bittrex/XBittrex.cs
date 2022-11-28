using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CCXT.Simple.Exchanges.Bittrex
{
    public class XBittrex : IExchange
    {
        /*
		 * Bittrex Support Markets: USDT,BTC
		 * 
		 * Rate Limit
		 *     https://bittrex.github.io/api/v3#/definitions/Ticker
		 * 
		 */

        public XBittrex(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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

        
        public string ExchangeName { get; set; } = "bittrex";

        public string ExchangeUrl { get; set; } = "https://api.bittrex.com";

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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.bittrex.com/v3/markets");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<Exchanges.Bittrex.Market>>(_jstring);

                    _queue_info.symbols.Clear();

                    foreach (var s in _jarray)
                    {
                        var _quote = s.quoteCurrencySymbol;

                        if (_quote == "USDT" || _quote == "USDC" || _quote == "USD" || _quote == "BTC")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.symbol,
                                compName = s.baseCurrencySymbol,
                                baseName = s.baseCurrencySymbol,
                                quoteName = _quote
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1305);
            }
            finally
            {
                this.Alive = _result;
            }

            return _result;
        }

        public async ValueTask CheckState(WStates states)
        {
            try
            {
                states.exchange = ExchangeName;

                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.bittrex.com/v3/currencies");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<Exchanges.Bittrex.Currency>>(_jstring);

                    foreach (var s in _jarray)
                    {
                        var _state = states.states.SingleOrDefault(x => x.currency == s.symbol);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = s.symbol,
                                active = s.status == "ONLINE",
                                deposit = true,
                                withdraw = true,
                                networks = new List<WNetwork>()
                            };

                            states.states.Add(_state);
                        }

                        var _cointype = s.coinType;
                        var _protocol = s.name;
                        if (s.coinType.StartsWith("ETH_"))
                        {
                            _cointype = "ETH";
                            if (s.notice.Contains("ERC-20"))
                                _protocol = "ERC20";
                        }

                        var _network = new WNetwork
                        {
                            name = s.symbol + "-" + s.coinType,
                            network = _cointype,
                            protocol = _protocol,

                            deposit = true,
                            withdraw = true,
                            
                            withdrawFee = s.txFee,
                            minWithdrawal = 0,
                            maxWithdrawal = 0,

                            minConfirm = s.minConfirmations,
                            arrivalTime = 0
                        };

                        _state.networks.Add(_network);
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 1306);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1307);
            }
        }

        /// <summary>
        /// Get Bittrex Tickers
        /// </summary>
        /// <param name="coin_names"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.bittrex.com/v3/markets/tickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jdata = JArray.Parse(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("lastTradeRate");
                            {
                                var _ask_price = _jitem.Value<decimal>("askRate");
                                var _bid_price = _jitem.Value<decimal>("bidRate");

                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USDC" || _ticker.quoteName == "USD")
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1308);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1309);
            }

            return _result;
        }

        public async ValueTask<bool> GetVolumes(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.bittrex.com/v3/markets/summaries");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jdata = JArray.Parse(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _volume = _jitem.Value<decimal>("quoteVolume");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USDC" || _ticker.quoteName == "USD")
                                    _volume *= tickers.exchgRate;
                                else if (_ticker.quoteName == "BTC")
                                    _volume *= mainXchg.btc_krw_price;

                                _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                                var _curr_timestamp = CUnixTime.ConvertToUnixTimeMilli(_jitem.Value<DateTime>("updatedAt"));
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1310);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1311);
            }

            return _result;
        }


        public ValueTask<bool> GetBookTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> GetMarkets(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        public ValueTask<decimal> GetPrice(string symbol)
        {
            throw new NotImplementedException();
        }
    }
}