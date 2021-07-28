// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        #region Graphs

        private double GetBonusEZ(double effectiveBpm) {
            if      (effectiveBpm >= 0.0 && effectiveBpm < 180.0) { return 1.1  - (Math.Pow(effectiveBpm - 90.0 , 2.0) / 81000.0); }
            else if (effectiveBpm >= 180 && effectiveBpm < 315.0) { return 0.85 + (Math.Pow(effectiveBpm - 315.0, 2.0) / 121500.0); }
            else                                                  { return 0.85; }

        }

        private double GetBonusEZHD(double effectiveBpm) {
            if      (effectiveBpm >= 0.0   && effectiveBpm < 195.0) { return 1.3 - (Math.Pow(effectiveBpm,           2.0) / 126750.0); }
            else if (effectiveBpm >= 195.0 && effectiveBpm < 260.0) { return 0.9 + (Math.Pow((effectiveBpm - 260.0), 2.0) / 42250.0);  }
            else if (effectiveBpm >= 260.0 && effectiveBpm < 340.0) { return 0.9 + (Math.Pow((effectiveBpm - 260.0), 2.0) / 128000.0);  }
            else if (effectiveBpm >= 340.0 && effectiveBpm < 420.0) { return 1.0 - (Math.Pow((effectiveBpm - 420.0), 2.0) / 128000.0);  }
            else                                                    { return 1.0; }
        }

        private double GetBonusHD(double effectiveBpm) {
            if      (effectiveBpm >= 0.0   && effectiveBpm < 180.0) { return 1.3 - (Math.Pow(effectiveBpm          , 2.0) / 151200.0); }
            else if (effectiveBpm >= 180.0 && effectiveBpm < 210.0) { return 1.05 + ( (Math.Pow(effectiveBpm - 210.0, 2.0) / 25200.0) );}
            else if (effectiveBpm >= 210.0 && effectiveBpm < 240.0) { return 1.05 + ( (Math.Pow(effectiveBpm - 210.0, 2.0) / 36000.0) );}
            else if (effectiveBpm >= 240.0 && effectiveBpm < 330.0) { return 1.15 - ((Math.Pow(effectiveBpm - 330.0, 2.0) / 108000.0)); }
            else                                                    { return 1.15; }
        }

        private double GetBonusHR(double effectiveBpm) {
            if      (effectiveBpm >= 0.0 && effectiveBpm < 160) return 0.9 + (Math.Pow((effectiveBpm - 80.0), 2) / 64000.0);
            else if (effectiveBpm >= 160 && effectiveBpm < 320) return 1.2 - (Math.Pow((effectiveBpm - 320.0), 2) / 128000.0);
            else                                                return 1.2;
        }

        private double GetBonusHDHR(double effectiveBpm) {
            if      (effectiveBpm >= 0.0 && effectiveBpm < 120) return 1.3 - (Math.Pow(effectiveBpm,       2.0) / 90000.0);
            else if (effectiveBpm >= 120 && effectiveBpm < 150) return 1.1 + (Math.Pow(effectiveBpm - 150.0, 2)   / 22500.0);
            else if (effectiveBpm >= 150 && effectiveBpm < 180) return 1.1 + (Math.Pow(effectiveBpm - 150.0, 2)   / 18000.0);
            else if (effectiveBpm >= 180 && effectiveBpm < 240) return 1.25 - (Math.Pow(effectiveBpm - 240.0, 2)   / 36000.0);
            else                                                return 1.4;
        }



        #endregion

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
            Stopwatch watch = Stopwatch.StartNew();
            //storing the function first, this is to optimize the code a little to avoid having to run mod checks every object,
            //assigned to `d => 1.0` so that later it doesnt complain about unassigned local variables
            Func<double, double> svBonusFunction = d => 1.0;

            if ( mods.Any(m => m is ModHidden)      &&
                 !mods.Any(m => m is ModHardRock)   &&
                 !mods.Any(m => m is ModFlashlight) &&
                 !mods.Any(m => m is ModEasy))
                svBonusFunction = GetBonusHD;

            if ( mods.Any(m => m is ModHidden) &&
                 mods.Any(m => m is ModEasy)   &&
                 !mods.Any(m => m is ModFlashlight))
                svBonusFunction = GetBonusEZHD;

            if ( mods.Any(m => m is ModHardRock) &&
                 !mods.Any(m => m is ModHidden)  &&
                 !mods.Any(m => m is ModFlashlight))
                svBonusFunction = GetBonusHR;

            if (mods.Any(m => m is ModEasy)    &&
                !mods.Any(m => m is ModHidden) &&
                !mods.Any(m => m is ModFlashlight))
                svBonusFunction = GetBonusEZ;

            if (mods.Any(m => m is ModHidden)   &&
                mods.Any(m => m is ModHardRock) &&
                !mods.Any(m => m is ModFlashlight))
                svBonusFunction = GetBonusHDHR;

            List<double> results = new List<double>();

            foreach (HitObject hitObject in Beatmap.HitObjects)
            {
                double sv = this.Beatmap.ControlPointInfo.DifficultyPointAt(hitObject.StartTime).SpeedMultiplier;
                double bpm = this.Beatmap.ControlPointInfo.TimingPointAt(hitObject.StartTime).BPM;

                //double weighed = Math.Min(700.0 / this.Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier, bpm * sv);

                double note_sv = sv * bpm;
                double sv_bonus = 0;

                if (mods.Any(m => m is ModDoubleTime))
                    note_sv *= 1.5;

                if (mods.Any(m => m is ModHalfTime))
                    note_sv *= 0.75;

                sv_bonus = svBonusFunction(note_sv);

                results.Add(sv_bonus);
            }

            double result = this.GetAverage(results.ToArray()) * (this.Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier / 1.4);
            //special case for nomod for safety
            if (!mods.Any())
                result = 1.0;

            return result;
        }
        #endregion

        public override double Calculate(Dictionary<string, double> categoryDifficulty = null) {
            //Sets mods to the current score's Mods
            mods = Score.Mods;
            //Gets Effective Average (BPM * Slider Velocty) for Scroll Speed Calcuation
            double sv_bonus = this.GetEffectiveBpmAverage();
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

            double diffstrain = (Math.Pow(4.62 * Attributes.StarRating, 3) / 100);
            diffstrain *= length_bonus;
            diffstrain *= Math.Max(0.0, Math.Pow(((totalHits - (double)Score.Statistics.GetOrDefault(HitResult.Miss)) / totalHits) - 1.0 / 600.0, 2.0 * (double)Score.Statistics.GetOrDefault(HitResult.Miss)));
            diffstrain *= Score.Accuracy;
            diffstrain *= (mods.Any(m => m is ModFlashlight) ? length_bonus * 1.1 : 1.0);
            diffstrain *= sv_bonus;

            //Accuracy Strain Calculation

            //the formatting is inconsistent becuase for some reason doing the exact same thing on diff strain made different results somehow? idk why but ill leave it like that for now
            double accstrain = (3.2   * Math.Pow(Attributes.StarRating, 1.2))
                               * ((190 / (hit_window + 1.5)) - 1.3)
                               * Math.Pow(Score.Accuracy, 20)
                               * Math.Pow(base_length, 0.3)
                               * (mods.Any(m => m is ModHidden) ? 1.1 : 1.0);

            //Total Combined PP from Both
            double total_pp = Math.Pow(Math.Pow(diffstrain, 1.1) + Math.Pow(accstrain, 1.1), (1 / 1.1))
                              * (mods.Any(m => m is ModFlashlight) ? multiplier : 1.0)
                              * (mods.Any(m => m is ModHalfTime) ? 0.95 : 1.0);

            //Add it to Category difficulty (not sure if required but I'd rather do it
            categoryDifficulty?.Add("Strain", diffstrain);
            categoryDifficulty?.Add("Accuracy", accstrain);

            return total_pp;
        }
        /// <summary>
        /// Gets all Hits possible in a Map
        /// </summary>
        private int totalHits => Score.Statistics[HitResult.Great] + Score.Statistics[HitResult.Ok] + Score.Statistics[HitResult.Miss];

    }
}
