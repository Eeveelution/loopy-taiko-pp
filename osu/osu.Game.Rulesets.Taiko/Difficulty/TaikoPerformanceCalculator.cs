// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Difficulty.Skills;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Taiko.Difficulty
{
    public class TaikoPerformanceCalculator : PerformanceCalculator
    {
        protected new TaikoDifficultyAttributes Attributes => (TaikoDifficultyAttributes)base.Attributes;

        private Mod[] mods;

        public TaikoPerformanceCalculator(Ruleset ruleset, DifficultyAttributes attributes, ScoreInfo score)
            : base(ruleset, attributes, score)
        {
        }



        public override double Calculate(Dictionary<string, double> categoryDifficulty = null)
        {
            mods = Score.Mods;

            double base_length = Math.Log((totalHits + 1500.0) / 1500.0, 2.0) /*/ 10.0 + 1.0*/;

            double multiplier = 1.0;
            double overall_difficulty = Attributes.Beatmap.BeatmapInfo.BaseDifficulty.OverallDifficulty;
            double hit_window;

            if (mods.Any(m => m is ModNoFail))
                multiplier *= 0.9;
            if (mods.Any(m => m is ModHidden))
                multiplier *= 1.05;

            if (mods.Any(m => m is ModEasy))
            {
                multiplier         *=  0.9;
                overall_difficulty /= 2.0;
            }
            if (mods.Any(m => m is ModHardRock))
            {
                overall_difficulty = Math.Min(overall_difficulty * 1.4, 10.0);
            }
            
            hit_window = Math.Floor(-3 * overall_difficulty) + 49.5;

            if (mods.Any(m => m is ModDoubleTime))
                hit_window *= 2.0 / 3.0;
            if (mods.Any(m => m is ModHalfTime))
                hit_window *= 4.0 / 3.0;


            double diffstrain = (((Math.Pow((470.0 * Attributes.StarRating - 7.0),3.0)) / 100000000.0)
                                * ((base_length / 10.0) + 1.0)
                                * Math.Sqrt(((double)totalHits - (double)Score.Statistics.GetOrDefault(HitResult.Miss)) / (double)totalHits)
                                * Math.Pow(0.985 , (double)Score.Statistics.GetOrDefault(HitResult.Miss))
                                * Score.Accuracy)
                                * (mods.Any(m => m is ModHidden) ? 1.025 : 1.0)
                                * (mods.Any(m => m is ModFlashlight) ? 1.05 * ((base_length / 10) + 1) : 1.0);

            double accstrain =  (Math.Pow((150 / hit_window), 1.1) + (34.5 - hit_window) / 15)
                * Math.Pow(Score.Accuracy, 15)
                * Math.Pow(base_length, 0.3)
                * 3.75 * Math.Pow(Attributes.StarRating, 1.1);

            double total_pp = Math.Pow(Math.Pow(diffstrain, 1.1) + Math.Pow(accstrain, 1.1), (1 / 1.1)) * multiplier;

            return total_pp;
        }

        private int totalHits => Score.Statistics[HitResult.Great] + Score.Statistics[HitResult.Ok] + Score.Statistics[HitResult.Miss];

    }
}
