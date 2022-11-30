namespace CCXT.Simple.Exchanges.Bithumb
{
    public class Datum
    {
        public string coinTypeCd { get; set; }
        public string coinSymbolNm { get; set; }
        public string coinNmKr { get; set; }
        public string coinNmEn { get; set; }
        public string networkType { get; set; }
        public string outAvailableYn { get; set; }
        public string scndAddrYn { get; set; }
        public string scndAddrNmKr { get; set; }
        public string scndAddrNmEn { get; set; }
    }

    public class CoinInfor
    {
        public int error { get; set; }
        public string message { get; set; }     
        public List<Datum> data { get; set; }
    }
}