﻿namespace GW2EIEvtcParser.EIData
{
    public abstract class FormDecorationCombatReplayDescription : GenericAttachedDecorationCombatReplayDescription
    {
        public bool Fill { get; }
        public int GrowingEnd { get; }
        public string Color { get; }

        internal FormDecorationCombatReplayDescription(ParsedEvtcLog log, FormDecoration decoration, CombatReplayMap map) : base(log, decoration, map)
        {
            Fill = decoration.Filled;
            Color = decoration.Color;
            GrowingEnd = decoration.GrowingReverse ? -decoration.GrowingEnd : decoration.GrowingEnd;
        }

    }

}
