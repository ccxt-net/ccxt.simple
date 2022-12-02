using CCXT.Simple.Base;
using CCXT.Simple.Data;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace CCXT.Simple.Exchanges.Korbit
{
    public class XKorbit : IExchange
    {
        /*
		 * Korbit Support Markets: KRW, USDT, BTC
		 *
		 * REST API
		 *     https://apidocs.korbit.co.kr/#first_section
		 *
		 */

        public XKorbit(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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

        public string ExchangeName { get; set; } = "korbit";

        public string ExchangeUrl { get; set; } = "https://api.korbit.co.kr";

        public string ExchangeGqUrl { get; set; } = "https://ajax.korbit.co.kr";

        public bool Alive { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public string PassPhrase { get; set; }
        public Tickers Tickers { get; set; }

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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker/detailed/all");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _queue_info = this.mainXchg.GetQInfors(ExchangeName);

                    foreach (var s in _jobject)
                    {
                        var _symbol = s.Key;
                        var _pairs = _symbol.Split('_');

                        _queue_info.symbols.Add(new QueueSymbol
                        {
                            symbol = _symbol,
                            compName = _pairs[0].ToUpper(),
                            baseName = _pairs[0].ToUpper(),
                            quoteName = _pairs[1].ToUpper()
                        });
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2011);
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
        public async ValueTask<bool> VerifyStates(Tickers tickers)
        {
            var _result = false;

            try
            {
                var graphQLClient = new GraphQLHttpClient($"{ExchangeGqUrl}/graphql", new SystemTextJsonSerializer());

                var graphQLRequest = new GraphQLRequest
                {
                    Query = @"{
								currencies {
									id acronym name decimal: floatingPoint confirmationCount withdrawalMaxOut withdrawalMaxPerRequest withdrawalTxFee withdrawalMinOut
									services {
										deposit exchange withdrawal depositStatus exchangeStatus withdrawalStatus brokerStatus
									}
									addressExtraProps {
										extraAddressField regexFormat required
									}
									addressRegexFormat type
								}
                            }"
                };

                var graphQLResponse = await graphQLClient.SendQueryAsync<Exchanges.Korbit.State>(graphQLRequest);
                foreach (var c in graphQLResponse.Data.currencies)
                {
                    var _currency = c.acronym.ToUpper();

                    var _state = tickers.states.SingleOrDefault(x => x.currency == _currency);
                    if (_state == null)
                    {
                        _state = new WState
                        {
                            currency = _currency,
                            active = true,
                            deposit = c.services.deposit,
                            withdraw = c.services.withdrawal,
                            networks = new List<WNetwork>()
                        };

                        tickers.states.Add(_state);
                    }
                    else
                    {
                        _state.deposit = c.services.deposit;
                        _state.withdraw = c.services.withdrawal;
                    }

                    var _t_items = tickers.items.Where(x => x.baseName == _state.currency);
                    if (_t_items != null)
                    {
                        foreach (var t in _t_items)
                        {
                            t.active = _state.active;
                            t.deposit = _state.deposit;
                            t.withdraw = _state.withdraw;
                        }
                    }
                }

                _result = true;

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 2012);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2013);
            }

            return _result;
        }

        /// <summary>
        /// Get Last Price
        /// </summary>
        /// <returns></returns>
        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker?currency_pair=" + symbol);
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_tstring);

                    _result = _jobject.Value<decimal>("last");

                    Debug.Assert(_result != 0.0m);
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2014);
            }

            return _result;
        }

        /// <summary>
        /// Get Bithumb Tickers
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> GetTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker/detailed/all");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JObject.Parse(_jstring).Properties();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jticker = _jarray.SingleOrDefault(x => x.Name == _ticker.symbol);
                        if (_jticker != null)
                        {
                            var _last_price = _jticker.Value.Value<decimal>("last");
                            {
                                _ticker.lastPrice = _last_price;
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2015);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2016);
            }

            return _result;
        }

        /// <summary>
        /// Get Bithumb Best Book Tickers
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> GetBookTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker/detailed/all");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JObject.Parse(_jstring).Properties();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jticker = _jarray.SingleOrDefault(x => x.Name == _ticker.symbol);
                        if (_jticker != null)
                        {
                            var _last_price = _jticker.Value.Value<decimal>("last");
                            {
                                _ticker.lastPrice = _last_price;

                                _ticker.askPrice = _jticker.Value.Value<decimal>("ask");
                                _ticker.bidPrice = _jticker.Value.Value<decimal>("bid");
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2017);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2018);
            }

            return _result;
        }

        /// <summary>
        /// Get Bithumb Volumes
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> GetVolumes(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker/detailed/all");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JObject.Parse(_jstring).Properties();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jticker = _jarray.SingleOrDefault(x => x.Name == _ticker.symbol);
                        if (_jticker != null)
                        {
                            var _last_price = _jticker.Value.Value<decimal>("last");

                            var _volume = _jticker.Value.Value<decimal>("volume");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                _volume *= _last_price;
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2019);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2020);
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v1/ticker/detailed/all");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JObject.Parse(_jstring).Properties();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jticker = _jarray.SingleOrDefault(x => x.Name == _ticker.symbol);
                        if (_jticker != null)
                        {
                            var _last_price = _jticker.Value.Value<decimal>("last");
                            {
                                _ticker.lastPrice = _last_price;

                                _ticker.askPrice = _jticker.Value.Value<decimal>("ask");
                                _ticker.bidPrice = _jticker.Value.Value<decimal>("bid");
                            }

                            var _volume = _jticker.Value.Value<decimal>("volume");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                _volume *= _last_price;
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2021);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2022);
            }

            return _result;
        }
    }
}