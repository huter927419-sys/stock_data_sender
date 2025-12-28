using System;

namespace StockDataMQClient
{
    /// <summary>
    /// 码表数据记录
    /// </summary>
    public class MarketTableDataRecord
    {
        /// <summary>
        /// 股票代码（标准化格式，如 "SH600000"）
        /// </summary>
        public string StockCode { get; set; }
        
        /// <summary>
        /// 股票名称
        /// </summary>
        public string StockName { get; set; }
        
        /// <summary>
        /// 市场代码（0=深圳, 1=上海）
        /// </summary>
        public int MarketCode { get; set; }
        
        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }
}

