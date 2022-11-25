using CCXT.Simple.Data;

namespace CCXT.Simple.Exchanges
{
    public interface IExchange
    {
        string ExchangeName { get; set; }
        bool Alive { get; set; }

        ValueTask<bool> VerifyCoinNames();
        ValueTask CheckState(WStates states);

        ValueTask<bool> GetBookTickers(Tickers tickers);
        ValueTask<bool> GetMarkets(Tickers tickers);
        ValueTask<decimal> GetPrice(string symbol);
        ValueTask<bool> GetTickers(Tickers tickers);
        ValueTask<bool> GetVolumes(Tickers tickers);
    }
}