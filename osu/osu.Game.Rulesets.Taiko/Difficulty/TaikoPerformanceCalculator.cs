// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Difficulty.Skills;
using osu.Game.Rulesets.Taiko.Mods;
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

        private double GetAverage(double[] array) {
            double result = 0.0;

            foreach (double d in array)
            {
                result += d;
            }

            return result / (double) array.Length;
        }

        
        private double GetMedian(double[] array)
        {
            double[] tempArray = array;
            int count = tempArray.Length;

            Array.Sort(tempArray);

            double medianValue = 0;

            if (count % 2 == 0)
            {
                // count is even, need to get the middle two elements, add them together, then divide by 2
                double middleElement1 = tempArray[(count / 2) - 1];
                double middleElement2 = tempArray[(count / 2)];
                medianValue = (middleElement1 + middleElement2) / 2.0;
            }
            else
            {
                // count is odd, simply get the middle element.
                medianValue = tempArray[(count / 2)];
            }

            return medianValue;
        }

        #region SV Bonus Calculation

        private double GetEffectiveBpmAverage() {
            List<double> results = new List<double>();

            foreach (HitObject hitObject in Beatmap.HitObjects)
            {
                double sv = this.Beatmap.ControlPointInfo.DifficultyPointAt(hitObject.StartTime).SpeedMultiplier;
                double bpm = this.Beatmap.ControlPointInfo.TimingPointAt(hitObject.StartTime).BPM;

                double weighed = Math.Min(700.0 / this.Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier, bpm * sv);

                results.Add(weighed);
            }

            return this.GetAverage(results.ToArray()) * (this.Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier / 1.4);
        }

        private double GetEffectiveBpmMedian() {
            List<double> results = new List<double>();

            foreach (HitObject hitObject in Beatmap.HitObjects)
            {
                double sv = this.Beatmap.ControlPointInfo.DifficultyPointAt(hitObject.StartTime).SpeedMultiplier;
                double bpm = this.Beatmap.ControlPointInfo.TimingPointAt(hitObject.StartTime).BPM;

                double weighed = Math.Min(700.0 / this.Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier, bpm * sv);

                results.Add(weighed);
            }

            return this.GetMedian(results.ToArray()) * (this.Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier / 1.4);
        }

        #endregion

        public override double Calculate(Dictionary<string, double> categoryDifficulty = null)
        {
            //Gets Effective Average (BPM * Slider Velocty) for Scroll Speed Calcuation
            double average_sv = this.GetEffectiveBpmAverage();
            double median_sv = this.GetEffectiveBpmMedian();
#if DEBUG
            Console.WriteLine("effective bpm average: {0}", average_sv);
            Console.WriteLine("effective bpm median: {0}",  median_sv);
#endif
            //Sets mods to the current score's Mods
            mods = Score.Mods;
            //Length Bonus
            double base_length = Math.Log((totalHits + 1500.0) / 1500.0, 2.0);
            double length_bonus = (Math.Pow(base_length, 0.75) / 10.0) + 1.0;

            int modHardrock = (mods.Any(m => m is ModHardRock) ? 1 : 0);
            int modHidden = (mods.Any(m => m is ModHidden) ? 1 : 0);
            int modEasy = (mods.Any(m => m is ModEasy) ? 1 : 0);

            double mod_multiplier_count = Math.Pow(1.05, modHardrock + modHidden) * Math.Pow(0.9, modEasy);
            double multiplier = length_bonus * mod_multiplier_count;

            //Maps Overall Difficulty value, gets changed with the Easy, Half Time, Hard Rock and Double Time mods
            double overall_difficulty = Attributes.Beatmap.BeatmapInfo.BaseDifficulty.OverallDifficulty;
            //Hit window for 300 hits in milliseconds (Used because Attributes.GreatHitWindow is unreliable)
            double hit_window;

            //adjust Overall Difficulty Values
            if (mods.Any(m => m is ModEasy))
            {
                overall_difficulty /= 2.0;
            }
            //changes OD
            if (mods.Any(m => m is ModHardRock))
            {
                overall_difficulty = Math.Min(overall_difficulty * 1.4, 10.0);
            }
            //Calculate Hit window for 300 hits
            hit_window = Math.Floor(-3 * overall_difficulty) + 49.5;

            //Double Time and Half Time both reduce/increase the hit window for 300 hits, this can be seen here
            if (mods.Any(m => m is ModDoubleTime))
                hit_window *= 2.0 / 3.0;

            if (mods.Any(m => m is ModHalfTime))
                hit_window *= 4.0 / 3.0;

            double diffstrain = (Math.Pow((23 * Attributes.StarRating), 3.0) / 12500.0);
            diffstrain        *= length_bonus;
            diffstrain        *= Math.Pow((((double)totalHits - (double)Score.Statistics.GetOrDefault(HitResult.Miss)) / (double)totalHits), 10.0);
            diffstrain        *= Score.Accuracy;
            diffstrain        *= (mods.Any(m => m is ModFlashlight) ? ((Math.Pow(base_length, 0.75) / 10.0)) + 1.0 : 1.0);

                         //* effectiveBpmResultThing

                         //Accuracy Strain Calculation
            double accstrain = (3.75 * Math.Pow(Attributes.StarRating, 1.1))
                               * Math.Pow((230 / hit_window), 0.85)
                               * Math.Pow(Score.Accuracy, 20)
                               * Math.Pow(base_length, 0.3)
                               * (mods.Any(m => m is ModHidden) ? 1.1 : 1.0);

            //Total Combined PP from Both
            double total_pp = Math.Pow(Math.Pow(diffstrain, 1.1) + Math.Pow(accstrain, 1.1), (1 / 1.1)) * (mods.Any(m => m is ModFlashlight) ? multiplier : 1.0);

            //Add it to Category difficulty (not sure if required but I'd rather do it
            categoryDifficulty.Add("Strain", diffstrain);
            categoryDifficulty.Add("Accuracy", accstrain);

            return total_pp;
        }
        /// <summary>
        /// Gets all Hits possible in a Map
        /// </summary>
        private int totalHits => Score.Statistics[HitResult.Great] + Score.Statistics[HitResult.Ok] + Score.Statistics[HitResult.Miss];

    }
}
