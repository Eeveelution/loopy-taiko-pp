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

        #region Speed Calculation

        private double GetAverageSVWeighed()
        {
            List<DifficultyControlPoint> sliderVelocities = new List<DifficultyControlPoint>(Beatmap.ControlPointInfo.DifficultyPoints);
            //Iterator to keep track at what Timing point we're at
            int k = 0;
            //Iterate over every BPM Change (Part of a fix for apparition)
            foreach (TimingControlPoint point in Beatmap.ControlPointInfo.TimingPoints)
            {
                //Check if it's the last Timing Point
                if (this.Beatmap.ControlPointInfo.TimingPoints.Count != k - 1)
                {
                    //Get the Next Timing Point
                    TimingControlPoint nextPoint = this.Beatmap.ControlPointInfo.TimingPoints[k + 1];
                    //See if there are any Timing Points
                    List<DifficultyControlPoint> controlPoints = this.Beatmap.ControlPointInfo.DifficultyPoints.Where(timingPoint => timingPoint.Time >= point.Time && timingPoint.Time < nextPoint.Time).ToList();
                    //If there aren't any, add a 1.0 SV
                    if (!controlPoints.Any())
                    {
                        DifficultyControlPoint controlPoint = new DifficultyControlPoint();
                        controlPoint.SpeedMultiplier = 1.0;
                        sliderVelocities.Add(controlPoint);
                    }
                    k++;
                }
                else
                {
                    //See if there are any Timing Points
                    List<DifficultyControlPoint> controlPoints = this.Beatmap.ControlPointInfo.DifficultyPoints.Where(point => point.Time >= point.Time && point.Time < Beatmap.HitObjects[Beatmap.HitObjects.Count - 1].StartTime + 1).ToList();
                    //If there aren't any, add a 1.0 SV
                    if (!controlPoints.Any())
                    {
                        DifficultyControlPoint controlPoint = new DifficultyControlPoint();
                        controlPoint.SpeedMultiplier = 1.0;
                        sliderVelocities.Add(controlPoint);
                    }
                    k++;
                }
            }

            if (sliderVelocities.Count == 0) return GetAverageBpmWeighed();
            //Total Objects in the Map
            int totalObjects = Beatmap.HitObjects.Count;
            //Average BPM, this is getting returned
            double average = 0.0;

            //This is to check if there are Objects before the first Green Line
            IEnumerable<HitObject> objectFix = Beatmap.HitObjects.Where(hit => hit.StartTime >= 0 && hit.StartTime < sliderVelocities[0].Time);
            //If yes, treat them as 1.0 Timing Points
            if (objectFix.Any())
            {
                //Iterator to keep track at what "fake" timing point we're at
                int j = 0;
                //Get a List of all Timing Points between the beginning of the map and the first Slider Velocity Change
                List<TimingControlPoint> fakeControlPoints =
                    new List<TimingControlPoint>(Beatmap.ControlPointInfo.TimingPoints.Where(point => point.Time >= 0 && point.Time < sliderVelocities[0].Time));//Beatmap.ControlPointInfo.TimingPoints.Where(point => point.Time >= 0 && point.Time < sliderVelocities[0].Time);
                //Goes for each Fake Timing point as if it were a Green Line with 1.0 SV
                foreach (TimingControlPoint point in fakeControlPoints)
                {
                    //If this ins't the last timing point
                    if (fakeControlPoints.Count != j + 1)
                    {
                        TimingControlPoint nextPoint = fakeControlPoints[j + 1];
                        double weighedSv = 0.0;
                        //Gets how Many objects are affected by the Line
                        int affectedObjects = Beatmap.HitObjects.Where(hit => hit.StartTime >= point.Time && hit.StartTime < nextPoint.Time).Count();
                        //Calculates for 1.0 SV Timing point
                        weighedSv = Math.Min(point.BPM, 700 / Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier);
                        //Multiplies by Percentage of how many Objects are affected
                        weighedSv *= ((double)affectedObjects / (double)totalObjects);
                        //Add to the total
                        average += weighedSv;
                        j++;
                    }
                    //If it is the Last Timing Point
                    else
                    {
                        double weighedSv = 0.0;
                        //Gets the Rest of the Objects starting from the Last timing point to the first Green Line
                        int affectedObjects = Beatmap.HitObjects.Where(hit => hit.StartTime >= point.Time && hit.StartTime < sliderVelocities[0].Time).Count();

                        weighedSv = Math.Min(point.BPM, 700 / Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier);
                        weighedSv *= ((double)affectedObjects / (double)totalObjects);

                        average += weighedSv;
                        j++;
                    }
                }
            }
            //Iterator to keep track of which Timing Point we're at
            int i = 0;
            //Go through all actual Slider Velocities
            foreach (DifficultyControlPoint point in sliderVelocities)
            {
                //If this isn't the last Timing Point
                if (sliderVelocities.Count != i + 1)
                {
                    //Get the Next timing point to get a span from where to get objects
                    DifficultyControlPoint nextPoint = sliderVelocities[i + 1];
                    double weighedSv = 0.0;
                    //Calculate affected objects based on this timing points start time and the next timing points start time (This points end time)
                    int affectedObjects = Beatmap.HitObjects.Where(hit => hit.StartTime >= point.Time && hit.StartTime < nextPoint.Time).Count();
                    //Weighed Slider Velocity is always: BPM * Slider Velocity
                    weighedSv = Beatmap.ControlPointInfo.TimingPointAt(point.Time).BPM * point.SpeedMultiplier;
                    //After that calculation we cap the Value at 700, this is to Prevent overuse of Ninja Notes
                    weighedSv = Math.Min(weighedSv, 700 / Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier);
                    //Multiplies by Percentage of how many Objects are affected
                    weighedSv *= ((double)affectedObjects / (double)totalObjects);
#if DEBUG
                    //Debug log
                    Console.WriteLine($"{i}: weighed: {weighedSv} | sv: {point.SpeedMultiplier} | objects affected: {affectedObjects}");
#endif
                    //Add to the average
                    average += weighedSv;
                    i++;
                }
                //If this is the last Timing Point
                else
                {
                    double weighedSv = 0.0;
                    //Gets all Affected Objects from the Timing point until the very last Object in the Map
                    int affectedObjects = Beatmap.HitObjects.Where(hit => hit.StartTime >= point.Time && hit.StartTime < Beatmap.HitObjects[Beatmap.HitObjects.Count - 1].StartTime + 1).Count();
                    //Weighed Slider Velocity is always: BPM * Slider Velocity
                    weighedSv = Beatmap.ControlPointInfo.TimingPointAt(point.Time).BPM * point.SpeedMultiplier;
                    //After that calculation we cap the Value at 700, this is to Prevent overuse of Ninja Notes
                    weighedSv = Math.Min(weighedSv, 700 / Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier);
                    //Multiplies by Percentage of how many Objects are affected
                    weighedSv *= ((double)affectedObjects / (double)totalObjects);
#if DEBUG
                    //Debug log
                    Console.WriteLine($"{i}: weighed: {weighedSv} | sv: {point.SpeedMultiplier} | objects affected: {affectedObjects}");
#endif
                    //Add to the average
                    average += weighedSv;
                    i++;
                }
            }
            return average * (Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier / 1.4);
        }
    
        private double GetAverageBpmWeighed()
        {
            if (Beatmap.ControlPointInfo.TimingPoints.Count == 1) return Beatmap.ControlPointInfo.TimingPoints[0].BPM * (Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier / 1.4);
            //Total Objects in the Map
            int totalObjects = Beatmap.HitObjects.Count;
            //Average BPM, this is getting returned
            double average = 0.0;
            int j = 0;

            IEnumerable<HitObject> objectFix = Beatmap.HitObjects.Where(hit => hit.StartTime >= 0 && hit.StartTime < Beatmap.ControlPointInfo.TimingPoints[0].Time);

            if(objectFix.Any())
            {
                double weighedSv = Math.Min(Beatmap.ControlPointInfo.TimingPoints[0].BPM, 700 / Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier);
                weighedSv *= ((double)objectFix.Count() / (double)totalObjects);

                average += weighedSv;
            }

            foreach (TimingControlPoint point in Beatmap.ControlPointInfo.TimingPoints)
            {
                //If this ins't the last timing point
                if (Beatmap.ControlPointInfo.TimingPoints.Count != j + 1)
                {
                    TimingControlPoint nextPoint = Beatmap.ControlPointInfo.TimingPoints[j + 1];
                    double weighedSv = 0.0;
                    //Gets how Many objects are affected by the Line
                    int affectedObjects = Beatmap.HitObjects.Where(hit => hit.StartTime >= point.Time && hit.StartTime < nextPoint.Time).Count();
                    //Calculates for 1.0 SV Timing point
                    weighedSv = Math.Min(point.BPM, 700 / Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier);
                    //Multiplies by Percentage of how many Objects are affected
                    weighedSv *= ((double)affectedObjects / (double)totalObjects);
#if DEBUG
                    Console.WriteLine($"{j}: weighed: {weighedSv} | bpm: {point.BPM} | objects affected: {affectedObjects}");
#endif
                    //Add to the total
                    average += weighedSv;
                    j++;
                }
                //If it is the Last Timing Point
                else
                {
                    double weighedSv = 0.0;
                    //Gets the Rest of the Objects starting from the Last timing point to the first Green Line
                    int affectedObjects = Beatmap.HitObjects.Where(hit => hit.StartTime >= point.Time && hit.StartTime < Beatmap.HitObjects[Beatmap.HitObjects.Count - 1].StartTime + 1).Count();

                    weighedSv = Math.Min(point.BPM, 700 / Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier);
                    weighedSv *= ((double)affectedObjects / (double)totalObjects);

#if DEBUG
                    Console.WriteLine($"{j}: weighed: {weighedSv} | bpm: {point.BPM} | objects affected: {affectedObjects}");
#endif

                    average += weighedSv;
                    j++;
                }
            }
            return average * (Beatmap.BeatmapInfo.BaseDifficulty.SliderMultiplier / 1.4);
        }

        #endregion

        public override double Calculate(Dictionary<string, double> categoryDifficulty = null)
        {
            //Gets Effective Average (BPM * Slider Velocty) for Scroll Speed Calcuation
            double average_sv = GetAverageSVWeighed();
#if DEBUG
            Console.WriteLine("effective bpm: {0}", average_sv);
#endif
            //Sets mods to the current score's Mods
            mods = Score.Mods;
            //Length Bonus
            double base_length = Math.Log((totalHits + 1500.0) / 1500.0, 2.0) /*/ 10.0 + 1.0*/;
            //Mod Multiplier
            double multiplier = 1.0;
            //Maps Overall Difficulty value, gets changed with the Easy, Half Time, Hard Rock and Double Time mods
            double overall_difficulty = Attributes.Beatmap.BeatmapInfo.BaseDifficulty.OverallDifficulty;
            //Hit window for 300 hits in milliseconds (Used because Attributes.GreatHitWindow is unreliable)
            double hit_window;
            //Sets Multiplier for Nofail, Nofail Nerfs a score by 10%
            if (mods.Any(m => m is ModNoFail))
                multiplier *= 0.9;

            //Sets Multiplier for Hidden, Hidden buffs a score by a straight 5%
            if (mods.Any(m => m is ModHidden))
                multiplier *= 1.1;

            //Apply Multiplier for Easy which has the same 10% as Nofail, and adjust Overall Difficulty Values
            if (mods.Any(m => m is ModEasy))
            {
                multiplier         *= 0.9;
                overall_difficulty /= 2.0;
            }

            //Hardrock gets a 7% buff and changes OD
            if (mods.Any(m => m is ModHardRock))
            {
                overall_difficulty =  Math.Min(overall_difficulty * 1.4, 10.0);
                multiplier         *= 1.07;
            }
            //Calculate Hit window for 300 hits
            hit_window = Math.Floor(-3 * overall_difficulty) + 49.5;

            //Double Time and Half Time both reduce/increase the hit window for 300 hits, this can be seen here
            if (mods.Any(m => m is ModDoubleTime))
                hit_window *= 2.0 / 3.0;

            if (mods.Any(m => m is ModHalfTime))
                hit_window *= 4.0 / 3.0;

            //Difficulty strain calculation
            double diffstrain = (((Math.Pow((470.0 * Attributes.StarRating - 7.0),3.0)) / 100000000.0)
                                * ((base_length / 10.0) + 1.0)
                                * Math.Sqrt(((double)totalHits - (double)Score.Statistics.GetOrDefault(HitResult.Miss)) / (double)totalHits)
                                * Math.Pow(0.985 , (double)Score.Statistics.GetOrDefault(HitResult.Miss))
                                * Score.Accuracy)
                                * (mods.Any(m => m is ModHidden) ? 1.025 : 1.0)
                                * (mods.Any(m => m is ModFlashlight) ? 1.05 * ((base_length / 10) + 1) : 1.0);

            //Accuracy Strain Calculation
            double accstrain =  (Math.Pow((150 / hit_window), 1.1) + (34.5 - hit_window) / 15)
                * Math.Pow(Score.Accuracy, 15)
                * Math.Pow(base_length, 0.3)
                * 3.75 * Math.Pow(Attributes.StarRating, 1.1);

            //Total Combined PP from Both
            double total_pp = Math.Pow(Math.Pow(diffstrain, 1.1) + Math.Pow(accstrain, 1.1), (1 / 1.1)) * multiplier;

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
