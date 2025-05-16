using System;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None, AddIndicators = true)]
    public class StochDivergenceBot : Robot
    {
        [Parameter("WMA Period", MinValue = 1)]
        public int WmaPeriod { get; set; }
        
        public float Slope { get; set; }
        private WeightedMovingAverage _wma;

        protected override void OnStart()
        {
            Print("The stoch divergence bot has started.");
            _wma = Indicators.WeightedMovingAverage(Bars.ClosePrices, WmaPeriod);
        }

        protected override void OnBar()
        {
            //if (((float)_wma.Result.LastValue - (float)_wma.Result.Last(2)) * Slope < 0)
            if (((float)_wma.Result.LastValue - (float)_wma.Result.Last(2)) * Slope < 0)
            {
                if (Slope < 0)
                {
                    Chart.DrawIcon("SlopeChange_" + Bars.OpenTimes.Last(2), ChartIconType.Star, Bars.OpenTimes.Last(2), MarketSeries.Close.Last(2), Color.Yellow);
                }
                
                if (Slope > 0)
                {
                    Chart.DrawIcon("SlopeChange_" + Bars.OpenTimes.Last(2), ChartIconType.Star, Bars.OpenTimes.Last(2), MarketSeries.Close.Last(2), Color.PowderBlue);
                }
            }
            Slope = (float)_wma.Result.LastValue - (float)_wma.Result.Last(2);
            
        }

        protected override void OnStop()
        {
            // Handle cBot stop here
        }
    }
}