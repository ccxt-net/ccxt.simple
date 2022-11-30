using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;

namespace CCXT.Simple.Exchanges.GateIO
{
    public class XGateIO : IExchange
    {
        /*
		 * Gate Support Markets: USDT, BTC
		 * 
		 * REST API
		 *     https://www.gate.io/docs/developers/apiv4/en/#retrieve-ticker-information
		 * 
		 */

        public XGateIO(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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
        
        public string ExchangeName { get; set; } = "gateio";

        public string ExchangeUrl { get; set; } = "https://api.gateio.ws";

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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.gateio.ws/api/v4/spot/currency_pairs");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<Exchanges.GateIO.Market>>(_jstring);

                    _queue_info.symbols.Clear();

                    foreach (var s in _jarray)
                    {
                        if (s.quote == "USDT" || s.quote == "USD" || s.quote == "BTC")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.id,
                                compName = s.@base,
                                baseName = s.@base,
                                quoteName = s.quote,
                                tickSize = s.min_quote_amount
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1805);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.gateio.ws/api/v4/spot/currencies");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<Exchanges.GateIO.Currency>>(_jstring);

                    foreach (var c in _jarray)
                    {
                        var _state = states.states.SingleOrDefault(x => x.currency == c.currency);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = c.currency,
                                active = !c.trade_disabled,
                                deposit = !c.deposit_disabled,
                                withdraw = !c.withdraw_disabled,
                                networks = new List<WNetwork>()
                            };

                            states.states.Add(_state);
                        }
                        else
                        {
                            _state.active = !c.trade_disabled;
                            _state.deposit = !c.deposit_disabled;
                            _state.withdraw = !c.withdraw_disabled;
                        }

                        var _name = c.currency + "-" + c.chain;

                        var _network = _state.networks.SingleOrDefault(x => x.name == _name);
                        if (_network == null)
                        {
                            _network = new WNetwork
                            {
                                name = _name,
                                network = c.chain,
                                protocol = c.chain,

                                deposit = !c.deposit_disabled,
                                withdraw = !c.withdraw_disabled,

                                withdrawFee = 0,
                                minWithdrawal = 0,
                                maxWithdrawal = 0,

                                minConfirm = 0
                            };

                            _state.networks.Add(_network);
                        }
                        else
                        {
                            _network.deposit = !c.deposit_disabled;
                            _network.withdraw = !c.withdraw_disabled;
                        }
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 1806);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1807);
            }
        }

        public async ValueTask<bool> GetMarkets(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.gateio.ws/api/v4/spot/tickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<RaTicker>>(_jstring, mainXchg.JsonSettings);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jarray.SingleOrDefault(x => x.currency_pair == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.last;
                            {
                                var _ask_price = _jitem.lowest_ask;
                                var _bid_price = _jitem.highest_bid;

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

                            var _volume = _jitem.quote_volume;
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USD")
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1808);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1809);
            }

            return _result;
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