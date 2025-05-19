using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None, AddIndicators = true)]
    public class draftstoch : Robot
    {

        // Parameters //
        [Parameter("WMA Period", MinValue = 1)]
        public int WmaPeriod { get; set; }
        
        // Indicators //
        private WeightedMovingAverage _wma;
        private StochasticOscillator _oscillator;
        
        // Classes //
        public class CriticalPoint
        {
            public DateTime TimePoint { get; set; }
            public double Value { get; set; }
            public int Type { get; set; }
            public CriticalPoint Pair { get; set; }

            public CriticalPoint(int type, double value, Robot robot)
            {
                this.Type = type;
                this.Value = value;
                this.TimePoint = robot.Bars.OpenTimes.Last(3);
                this.Pair = null;
            }
        }
        
        // Variables //

        public bool inLong = false;
        public bool inShort = false;
        
        // holds slopes
        public double wmaSlope;
        public double oscillatorSlope;
        
        // holds all critical points
        CriticalPoint[] StochasticCP = new CriticalPoint[8];
        CriticalPoint[] PriceCP = new CriticalPoint[8];

        // holds all paired critical points
        CriticalPoint[] PairedStochasticCP = new CriticalPoint[8];
        CriticalPoint[] PairedPriceCP = new CriticalPoint[8];
        
        // holds all type 1 critical points
        CriticalPoint[] PairedStochasticCPType1 = new CriticalPoint[8];
        CriticalPoint[] PairedPriceCPType1 = new CriticalPoint[8];
        
        // holds all type 2 critical points
        CriticalPoint[] PairedStochasticCPType2 = new CriticalPoint[8];
        CriticalPoint[] PairedPriceCPType2 = new CriticalPoint[8];
        
        // Custom Functions //
        public CriticalPoint[] AppendOnRotation(CriticalPoint data, CriticalPoint[] original)
        {
            CriticalPoint[] FinalResult = new CriticalPoint[original.Length];
            for (int i = 0; i < original.Length - 1; i++)
            {
                FinalResult[i] = original[i + 1];
            }

            FinalResult[original.Length - 1] = data;
            
            return FinalResult;
        }
        
        public bool isCP(ref double givenSlope, double previousValue, double ancestorValue, ref CriticalPoint[] targetArray, string indicator)
        {
            if ((previousValue - ancestorValue) * givenSlope < 0)
            {
                int typeOfPoint = 0;
                
                if (givenSlope < 0)
                {
                    typeOfPoint = 1; // min
                }
                else
                {
                    typeOfPoint = 2; // max
                }

                CriticalPoint newCP;
                if (indicator == "oscillator")
                {
                     newCP = new CriticalPoint(typeOfPoint, _oscillator.PercentK.Last(2), this);
                }
                else if (indicator == "wma")
                {
                    newCP = new CriticalPoint(typeOfPoint, _wma.Result.Last(2), this);
                }
                else
                {
                    newCP = null;
                }
                
                targetArray = AppendOnRotation(newCP, targetArray);

                givenSlope = previousValue - ancestorValue;
                return true;
            }

            givenSlope = previousValue - ancestorValue;
            return false;
        }

        public List<CriticalPoint> isolateType(CriticalPoint[] targetList, int typeDesired)
        {
            List<CriticalPoint> returnList = new List<CriticalPoint>();

            foreach (var CP in targetList)
            {
                if (CP.Type == typeDesired)
                {
                    returnList.Add(CP);
                }
            }

            return returnList;
        }

        // Default Function //

        protected override void OnStart()
        {
            _wma = Indicators.WeightedMovingAverage(Bars.ClosePrices, WmaPeriod);
            _oscillator = Indicators.StochasticOscillator(9, 1, 3, MovingAverageType.Simple);
        }

        protected override void OnBar()
        {
            isCP(ref wmaSlope, _wma.Result.LastValue, _wma.Result.Last(2), ref PriceCP, "wma");
            isCP(ref oscillatorSlope, _oscillator.PercentK.LastValue, _oscillator.PercentK.Last(2), ref StochasticCP, "oscillator");
            
            
            // Define Pairs
            List<CriticalPoint> wmaPairedList = new List<CriticalPoint>();
            List<CriticalPoint> stochPairedList = new List<CriticalPoint>();

            for (int i = 0; i < PriceCP.Length; i++)
            {
                TimeSpan difference = PriceCP[i].TimePoint - StochasticCP[i].TimePoint;
                if (Math.Abs(difference.TotalHours) <= 2)
                {
                    wmaPairedList.Add(PriceCP[i]);
                    stochPairedList.Add(StochasticCP[i]);
                }
            }

            PairedPriceCP = wmaPairedList.ToArray();
            PairedStochasticCP = stochPairedList.ToArray();

            // Look for buys //
            PairedPriceCPType1 = isolateType(PairedPriceCP, 1).ToArray();
            PairedStochasticCPType1 = isolateType(PairedStochasticCP, 1).ToArray();
            
            List<double> PairedPriceCPType1Slopes = new List<double>();
            List<double> PairedStochasticCPType1Slopes = new List<double>();

            for (int i = 0; i < PairedPriceCPType1.Length - 1; i++)
            {
                double slope = PairedPriceCPType1[PairedPriceCPType1.Length - 1].Value - PairedPriceCPType1[i].Value;
                PairedPriceCPType1Slopes.Add(slope);
            }
            
            for (int i = 0; i < PairedStochasticCPType1.Length - 1; i++)
            {
                double slope = PairedStochasticCPType1[PairedStochasticCPType1.Length - 1].Value - PairedStochasticCPType1[i].Value;
                PairedStochasticCPType1Slopes.Add(slope);
            }

            double averageSlopePrice = PairedPriceCPType1Slopes.Sum();
            double averageStochPrice = PairedStochasticCPType1Slopes.Sum();

            double stopLossPips = 4000.0;
            
            if ((averageSlopePrice < 0 && averageStochPrice > 0) && _oscillator.PercentK.LastValue > 25.00 && _oscillator.PercentK.LastValue < 70.00 && inLong == false && inShort == false)
            {
                var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, 250.0, "XAUUSD");
                
                if (result.IsSuccessful)
                {
                    var position = result.Position;
                    double stopLossPrice = position.EntryPrice - (stopLossPips * Symbol.PipSize);
                    ModifyPosition(position, stopLossPrice, position.TakeProfit);
                    Print($"Position entry price is {position.EntryPrice}");
                    inLong = true;
                }
            }

            // Look for sells //
        }

        protected override void OnTick()
        {
            if (inLong && _oscillator.PercentK.LastValue >= 80)
            {
                var position = Positions.Find("XAUUSD"); // Find the position
                if (position != null)
                {
                    ClosePosition(position);
                    Print("Position closed.");
                    inLong = false;
                }
            }
        }
        
        protected override void OnPositionClosed(Position position)
        {
            if (position.SymbolName == SymbolName && position.TradeType == TradeType.Buy)
            {
                Print("Position closed. Checking reason...");
        
                // Check if the position was closed due to stop loss
                if (position.StopLoss.HasValue && position.GrossProfit <= 0)
                {
                    Print("Stop loss triggered. Setting inLong to false.");
                    inLong = false;
                }
            }
        }

        protected override void OnStop()
        {

        }
    }
}
